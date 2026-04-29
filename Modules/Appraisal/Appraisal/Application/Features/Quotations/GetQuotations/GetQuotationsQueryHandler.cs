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
        SELECT q.Id, q.QuotationNumber, q.RequestDate, q.DueDate, q.Status, q.RequestedBy,
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

        // Optional filter: free-text search across QuotationNumber and RequestedBy
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            sql += " AND (q.QuotationNumber LIKE @SearchPattern OR q.RequestedBy LIKE @SearchPattern)";
            dynamicParams.Add("SearchPattern", "%" + query.Search.Trim() + "%");
        }

        // Optional filter: DueDate range — convert DateOnly → DateTime (Dapper rejects DateOnly)
        if (query.DueDateFrom.HasValue)
        {
            sql += " AND q.DueDate >= @DueDateFrom";
            dynamicParams.Add("DueDateFrom", query.DueDateFrom.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (query.DueDateTo.HasValue)
        {
            sql += " AND q.DueDate <= @DueDateTo";
            dynamicParams.Add("DueDateTo", query.DueDateTo.Value.ToDateTime(TimeOnly.MaxValue));
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
