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
    // Explicit column list matches the QuotationDto positional record constructor exactly.
    // RmUserId and AppraisalId are used for filtering only — not selected into the DTO.
    private const string SelectColumns = """
        SELECT q.Id, q.QuotationNumber, q.RequestDate, q.CutOffTime, q.Status, q.RequestedBy,
               q.TotalAppraisals, q.TotalCompaniesInvited, q.TotalQuotationsReceived
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
            sql = $"{SelectColumns} FROM appraisal.vw_QuotationList q WHERE 1 = 1";
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

        // Optional filter: Status
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            sql += " AND q.Status = @Status";
            dynamicParams.Add("Status", query.Status.Trim());
        }

        // Optional free-text search: matches quotation number, linked appraisal number,
        // or customer name (via the appraisal → request chain). Appraisal/customer are
        // OR-ed in as EXISTS subqueries so the outer statement stays wrappable for COUNT.
        //
        // Performance: all three columns (QuotationNumber, Appraisals.AppraisalNumber,
        // RequestCustomers.Name) have indexes tuned for a PREFIX seek. So the default is a
        // prefix match ("term%") which seeks the index. A leading-wildcard contains-search
        // scans every row, so it is opt-in: the user types '*' wherever they want a wildcard
        // (e.g. "*abc" → "%abc", "ab*cd" → "ab%cd", "*abc*" → "%abc%").
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            sql += """
                 AND (
                     q.QuotationNumber LIKE @SearchPattern
                     OR EXISTS (
                         SELECT 1 FROM appraisal.QuotationRequestAppraisals qra
                         JOIN appraisal.Appraisals a ON a.Id = qra.AppraisalId
                         WHERE qra.QuotationRequestId = q.Id
                           AND a.AppraisalNumber LIKE @SearchPattern
                     )
                     OR EXISTS (
                         SELECT 1 FROM appraisal.QuotationRequestAppraisals qra
                         JOIN appraisal.Appraisals a ON a.Id = qra.AppraisalId
                         JOIN request.RequestCustomers rc ON rc.RequestId = a.RequestId
                         WHERE qra.QuotationRequestId = q.Id
                           AND rc.Name LIKE @SearchPattern
                     )
                 )
                """;
            var term = query.Search.Trim();
            var pattern = term.Contains('*') ? term.Replace('*', '%') : term + "%";
            dynamicParams.Add("SearchPattern", pattern);
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
            "RequestDate DESC",
            query.PaginationRequest,
            dynamicParams);

        return new GetQuotationsResult(result);
    }

    private static GetQuotationsResult EmptyResult(PaginationRequest request) =>
        new(new PaginatedResult<QuotationDto>([], 0, request.PageNumber, request.PageSize));
}
