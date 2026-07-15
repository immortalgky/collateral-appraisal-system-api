using Common.Application.Features.Monitoring.Shared;
using Dapper;
using Shared.CQRS;
using Shared.Data;

namespace Common.Application.Features.Monitoring.GetPendingEvaluations;

/// <summary>
/// Returns a Total count for the Pending Evaluations monitoring tab.
/// Applies the same Pending/Draft pre-filter as the list handler.
/// Bucket fields (Breached/AtRisk/Healthy) are null — no OLA data on the evaluation view.
/// </summary>
public class GetPendingEvaluationsSummaryQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetPendingEvaluationsSummaryQuery, MonitoringSummaryDto>
{
    public async Task<MonitoringSummaryDto> Handle(
        GetPendingEvaluationsSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        var filter = query.Filter;

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

        if (!string.IsNullOrWhiteSpace(filter.InternalFollowupStaff))
        {
            conditions.Add("InternalFollowupStaffId = @InternalFollowupStaff");
            parameters.Add("InternalFollowupStaff", filter.InternalFollowupStaff.Trim());
        }

        var where = "WHERE " + string.Join(" AND ", conditions);
        var sql = $"SELECT COUNT(*) FROM appraisal.vw_AppraisalEvaluationList {where}";

        var conn = connectionFactory.GetOpenConnection();
        var total = await conn.ExecuteScalarAsync<int>(sql, parameters);
        return new MonitoringSummaryDto(total, null, null, null);
    }

    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
}
