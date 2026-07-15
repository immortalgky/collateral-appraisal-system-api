using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Pagination;

namespace Common.Application.Features.Monitoring.GetPendingEvaluations;

/// <summary>
/// Returns pending appraisal company evaluations from appraisal.vw_AppraisalEvaluationList (reused as-is).
/// Admin-only screen — no activity-ID scoping needed.
/// </summary>
public class GetPendingEvaluationsQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetPendingEvaluationsQuery, PaginatedResult<PendingEvaluationDto>>
{
    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "AppraisalNumber", "CustomerName", "EvaluationStatus", "ReportReceivedDate",
        "AppraiserCompanyName", "TotalScore", "AppraisalValue"
    };

    public async Task<PaginatedResult<PendingEvaluationDto>> Handle(
        GetPendingEvaluationsQuery query,
        CancellationToken cancellationToken)
    {
        // Filter to non-completed evaluations (Pending or Draft) for the monitoring surface.
        // Explicit projection — column order MUST match PendingEvaluationDto's positional record constructor.
        var sql = @"
SELECT
    AppraisalId,
    AppraisalNumber,
    AppraisalStatus,
    CustomerName,
    ReportReceivedDate,
    ExternalAppraiserName,
    AssigneeCompanyId,
    AppraiserCompanyName,
    AppraisalValue,
    EvaluationId,
    EvaluationStatus,
    TotalScore,
    InternalFollowupStaffId,
    InternalFollowupStaffName
FROM appraisal.vw_AppraisalEvaluationList";
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        var filter = query.Filter;

        // If caller specifies statuses, filter to exactly those; otherwise default to Pending only.
        if (filter.EvaluationStatus is { Length: > 0 })
        {
            conditions.Add("EvaluationStatus IN @EvaluationStatuses");
            parameters.Add("EvaluationStatuses", filter.EvaluationStatus);
        }
        else
        {
            conditions.Add("EvaluationStatus = 'Pending'");
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            conditions.Add("(AppraisalNumber LIKE @Search ESCAPE '\\' OR CustomerName LIKE @Search ESCAPE '\\' OR AppraiserCompanyName LIKE @Search ESCAPE '\\')");
            parameters.Add("Search", "%" + EscapeLike(filter.Search.Trim()) + "%");
        }

        if (!string.IsNullOrWhiteSpace(filter.AppraisalCompanyId))
        {
            conditions.Add("AssigneeCompanyId = @AppraisalCompanyId");
            parameters.Add("AppraisalCompanyId", filter.AppraisalCompanyId);
        }

        if (filter.AppraisalStatus is { Length: > 0 })
        {
            conditions.Add("AppraisalStatus IN @AppraisalStatuses");
            parameters.Add("AppraisalStatuses", filter.AppraisalStatus);
        }

        // Exact match on the internal followup staff username (autocomplete-selected value).
        if (!string.IsNullOrWhiteSpace(filter.InternalFollowupStaff))
        {
            conditions.Add("InternalFollowupStaffId = @InternalFollowupStaff");
            parameters.Add("InternalFollowupStaff", filter.InternalFollowupStaff.Trim());
        }

        sql += " WHERE " + string.Join(" AND ", conditions);

        var sortField = AllowedSortFields.Contains(filter.SortBy ?? "") ? filter.SortBy! : "ReportReceivedDate";
        var sortDir = string.Equals(filter.SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";
        var orderBy = $"{sortField} {sortDir}";

        return await connectionFactory.QueryPaginatedAsync<PendingEvaluationDto>(sql, orderBy, query.Paging, parameters);
    }

    // Escapes SQL Server LIKE wildcards so user input matches literally; paired with ESCAPE '\'.
    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
}
