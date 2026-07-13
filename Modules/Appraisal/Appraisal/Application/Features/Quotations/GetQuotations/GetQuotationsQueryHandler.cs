using Appraisal.Contracts.Services;
using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Identity;
using Shared.Pagination;

namespace Appraisal.Application.Features.Quotations.GetQuotations;

/// <summary>
/// Handler for getting Quotations with pagination.
/// Admin / IntAdmin: all quotations.
/// All other callers: scoped by pool-task ownership (active or completed) on the quotation-workflow.
/// Drafts and never-sent RFQs have no workflow rows and are therefore invisible to non-admins.
/// Uses SQL view + Dapper for efficient read queries.
/// </summary>
public class GetQuotationsQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser,
    IPoolTaskClauseService poolTaskClauseService
) : IQueryHandler<GetQuotationsQuery, GetQuotationsResult>
{
    // Explicit column list matches the QuotationDto record. RmUserId/AppraisalId are used for
    // filtering only. Customer columns come from the CustomerApply join below.
    private const string SelectColumns = """
        SELECT q.Id, q.QuotationNumber, q.RequestDate, q.CutOffTime, q.Status, q.RequestedBy,
               q.TotalAppraisals, q.TotalCompaniesInvited, q.TotalQuotationsReceived,
               cust.CustomerName, cust.CustomerCount, cust.CustomerNames
        """;

    // Distinct customer names for the quotation, via QuotationRequestAppraisals → Appraisals →
    // RequestCustomers (correlated on q.Id — the same chain the customer search filter uses).
    // Representative name = MIN; count + ordered full list drive the "+N" indicator and tooltip.
    private const string CustomerApply = """
        OUTER APPLY (
            SELECT CustomerName = MIN(x.Name), CustomerCount = COUNT(*),
                   CustomerNames = STRING_AGG(x.Name, ', ') WITHIN GROUP (ORDER BY x.Name)
            FROM (SELECT DISTINCT rc.Name
                  FROM appraisal.QuotationRequestAppraisals qra
                  JOIN appraisal.Appraisals a ON a.Id = qra.AppraisalId
                  JOIN request.RequestCustomers rc ON rc.RequestId = a.RequestId
                  WHERE qra.QuotationRequestId = q.Id) x
        ) cust
        """;

    // PoolTaskAccess.BuildSqlClause references AssigneeUserId / AssigneeCompanyId column names.
    // The physical columns in both tables are AssignedTo / AssigneeCompanyId, so we alias AssignedTo
    // as AssigneeUserId in derived subqueries.
    private const string PendingTasksSubquery =
        "(SELECT AssignedTo AS AssigneeUserId, AssigneeCompanyId, CorrelationId, AssignedType FROM workflow.PendingTasks)";

    private const string CompletedTasksSubquery =
        "(SELECT AssignedTo AS AssigneeUserId, AssigneeCompanyId, CorrelationId, AssignedType FROM workflow.CompletedTasks)";

    public async Task<GetQuotationsResult> Handle(
        GetQuotationsQuery query,
        CancellationToken cancellationToken)
    {
        string sql;
        var dynamicParams = new DynamicParameters();

        if (currentUser.IsInRole("Admin") || currentUser.IsInRole("IntAdmin"))
        {
            sql = $"{SelectColumns} FROM appraisal.vw_QuotationList q {CustomerApply} WHERE 1 = 1";
        }
        else
        {
            var clause = await poolTaskClauseService.BuildClauseForCurrentUserAsync(cancellationToken);
            if (clause is null)
                return EmptyResult(query.PaginationRequest);

            // Active OR historical task ownership on the quotation-workflow.
            // Drafts / never-sent RFQs have no rows in either table → invisible.
            // AssignedType filter is intentionally omitted: the candidate set in clause.Sql
            // already includes the caller's username (for direct-assignment tasks, type '1')
            // and group/team names (for pool tasks, type '2').  Filtering on type would hide
            // rm-pick-winner rows (type '1') and claimed-task rows (type '1') for RM callers.
            sql = $$"""
                {{SelectColumns}} FROM appraisal.vw_QuotationList q
                {{CustomerApply}}
                WHERE EXISTS (
                    SELECT 1 FROM {{PendingTasksSubquery}} pt
                    WHERE pt.CorrelationId = q.Id
                      AND {{clause.Sql}}
                    UNION ALL
                    SELECT 1 FROM {{CompletedTasksSubquery}} ct
                    WHERE ct.CorrelationId = q.Id
                      AND {{clause.Sql}}
                )
                """;
            foreach (var (k, v) in clause.Parameters)
                dynamicParams.Add(k, v);
        }

        // v2: filter by appraisal via the join table (AppraisalId column dropped)
        if (query.AppraisalId.HasValue)
        {
            sql += """
                 AND EXISTS (
                     SELECT 1 FROM appraisal.QuotationRequestAppraisals qra
                     WHERE qra.QuotationRequestId = q.Id
                       AND qra.AppraisalId = @AppraisalId
                 )
                """;
            dynamicParams.Add("AppraisalId", query.AppraisalId.Value);
        }

        // Optional filter: Status (multi-select → IN list; Dapper expands the array)
        if (query.Statuses is { Length: > 0 })
        {
            sql += " AND q.Status IN @Statuses";
            dynamicParams.Add("Statuses", query.Statuses);
        }

        // Optional per-field search. Each field is an INDEPENDENT predicate AND-ed onto the
        // query, so combining fields NARROWS the result set (e.g. quotation-no AND customer).
        // Splitting them (vs one OR-ed omnibox) keeps each predicate on a single column.
        // Each is a contains match ("%term%") — see BuildLikePattern for why that is cheap here.
        if (!string.IsNullOrWhiteSpace(query.QuotationNo))
        {
            sql += " AND q.QuotationNumber LIKE @QuotationNoPattern";
            dynamicParams.Add("QuotationNoPattern", BuildLikePattern(query.QuotationNo));
        }

        if (!string.IsNullOrWhiteSpace(query.AppraisalNo))
        {
            sql += """
                 AND EXISTS (
                     SELECT 1 FROM appraisal.QuotationRequestAppraisals qra
                     JOIN appraisal.Appraisals a ON a.Id = qra.AppraisalId
                     WHERE qra.QuotationRequestId = q.Id
                       AND a.AppraisalNumber LIKE @AppraisalNoPattern
                 )
                """;
            dynamicParams.Add("AppraisalNoPattern", BuildLikePattern(query.AppraisalNo));
        }

        if (!string.IsNullOrWhiteSpace(query.CustomerName))
        {
            sql += """
                 AND EXISTS (
                     SELECT 1 FROM appraisal.QuotationRequestAppraisals qra
                     JOIN appraisal.Appraisals a ON a.Id = qra.AppraisalId
                     JOIN request.RequestCustomers rc ON rc.RequestId = a.RequestId
                     WHERE qra.QuotationRequestId = q.Id
                       AND rc.Name LIKE @CustomerNamePattern
                 )
                """;
            dynamicParams.Add("CustomerNamePattern", BuildLikePattern(query.CustomerName));
        }

        // Optional filter: CutOffTime range — convert DateOnly → DateTime (Dapper rejects DateOnly)
        if (query.CutOffTimeFrom.HasValue)
        {
            sql += " AND q.CutOffTime >= @CutOffTimeFrom";
            dynamicParams.Add("CutOffTimeFrom", query.CutOffTimeFrom.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (query.CutOffTimeTo.HasValue)
        {
            sql += " AND q.CutOffTime <= @CutOffTimeTo";
            dynamicParams.Add("CutOffTimeTo", query.CutOffTimeTo.Value.ToDateTime(TimeOnly.MaxValue));
        }

        // Optional filter: invited company (exact CompanyId)
        if (query.CompanyId.HasValue)
        {
            sql += """
                 AND EXISTS (
                     SELECT 1 FROM appraisal.QuotationInvitations qi
                     WHERE qi.QuotationRequestId = q.Id
                       AND qi.CompanyId = @CompanyId
                 )
                """;
            dynamicParams.Add("CompanyId", query.CompanyId.Value);
        }

        var result = await connectionFactory.QueryPaginatedAsync<QuotationDto>(
            sql,
            BuildOrderBy(query.SortBy, query.SortDir),
            query.PaginationRequest,
            dynamicParams);

        return new GetQuotationsResult(result);
    }

    // Whitelist of user-sortable columns → real view columns. Only these literal strings ever
    // reach the ORDER BY, so user input can never inject. Keys are what the frontend sends.
    private static readonly Dictionary<string, string> AllowedSortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["QuotationNumber"] = "QuotationNumber",
        ["Status"] = "Status",
        ["CutOffTime"] = "CutOffTime",
        ["TotalAppraisals"] = "TotalAppraisals",
        ["RequestDate"] = "RequestDate",
        ["CustomerName"] = "cust.CustomerName",
    };

    // Default RequestDate DESC; Id is a stable tiebreaker (never a sort key → no duplicate-column trap).
    private static string BuildOrderBy(string? sortBy, string? sortDir)
    {
        var column = AllowedSortColumns.TryGetValue(sortBy ?? "", out var col) ? col : "RequestDate";
        var direction = string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";
        return $"{column} {direction}, Id DESC";
    }

    private static GetQuotationsResult EmptyResult(PaginationRequest request) =>
        new(new PaginatedResult<QuotationDto>([], 0, request.PageNumber, request.PageSize));

    // Contains match ("%term%") — matches anywhere in the field. On these listings this is
    // cheap: quotation number is a tiny table, and appraisal/customer ride a correlated EXISTS
    // driven from the small quotation set, so it never scans the large Appraisals /
    // RequestCustomers tables directly.
    private static string BuildLikePattern(string term) => $"%{term.Trim()}%";
}
