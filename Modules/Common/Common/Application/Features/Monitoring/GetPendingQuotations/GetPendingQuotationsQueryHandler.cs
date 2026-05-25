using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Pagination;

namespace Common.Application.Features.Monitoring.GetPendingQuotations;

/// <summary>
/// Returns pending quotations from appraisal.vw_QuotationList (reused as-is per constraint D5).
/// No activity-ID scoping — this is an admin-level screen with a single permission.
/// </summary>
public class GetPendingQuotationsQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetPendingQuotationsQuery, PaginatedResult<PendingQuotationDto>>
{
    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "QuotationNumber", "Status", "RequestDate", "CutOffTime", "RequestedBy", "RmUsername",
        "TotalAppraisals", "TotalCompaniesInvited", "TotalQuotationsReceived"
    };

    public async Task<PaginatedResult<PendingQuotationDto>> Handle(
        GetPendingQuotationsQuery query,
        CancellationToken cancellationToken)
    {
        // Explicit projection — column order MUST match PendingQuotationDto's positional record constructor.
        var sql = @"
SELECT
    Id,
    QuotationNumber,
    Status,
    RequestDate,
    CutOffTime,
    RequestedBy,
    TotalAppraisals,
    TotalCompaniesInvited,
    TotalQuotationsReceived,
    RmUsername
FROM appraisal.vw_QuotationList";
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        var filter = query.Filter;

        // Default: exclude terminal statuses to show only pending/open quotations.
        // If specific statuses are requested, skip the exclusion list and filter exactly.
        if (filter.Status is { Length: > 0 })
        {
            conditions.Add("Status IN @Statuses");
            parameters.Add("Statuses", filter.Status);
        }
        else
        {
            // Closed, Finalized, Cancelled are terminal states; exclude from the monitoring surface.
            conditions.Add("Status NOT IN ('Closed', 'Finalized', 'Cancelled')");
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            conditions.Add("(QuotationNumber LIKE @Search ESCAPE '\\' OR RequestedBy LIKE @Search ESCAPE '\\')");
            parameters.Add("Search", "%" + EscapeLike(filter.Search.Trim()) + "%");
        }

        // Optional filter: CutOffTime range — convert DateOnly → DateTime (Dapper rejects DateOnly)
        if (filter.CutOffTimeFrom.HasValue)
        {
            conditions.Add("CutOffTime >= @CutOffTimeFrom");
            parameters.Add("CutOffTimeFrom", filter.CutOffTimeFrom.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (filter.CutOffTimeTo.HasValue)
        {
            conditions.Add("CutOffTime <= @CutOffTimeTo");
            parameters.Add("CutOffTimeTo", filter.CutOffTimeTo.Value.ToDateTime(TimeOnly.MaxValue));
        }

        sql += " WHERE " + string.Join(" AND ", conditions);

        var sortField = AllowedSortFields.Contains(filter.SortBy ?? "") ? filter.SortBy! : "RequestDate";
        var sortDir = string.Equals(filter.SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";
        var orderBy = $"{sortField} {sortDir}";

        return await connectionFactory.QueryPaginatedAsync<PendingQuotationDto>(sql, orderBy, query.Paging, parameters);
    }

    // Escapes SQL Server LIKE wildcards so user input matches literally; paired with ESCAPE '\'.
    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
}
