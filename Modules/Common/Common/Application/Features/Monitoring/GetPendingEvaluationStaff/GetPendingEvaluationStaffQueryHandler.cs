using Dapper;
using Shared.CQRS;
using Shared.Data;

namespace Common.Application.Features.Monitoring.GetPendingEvaluationStaff;

/// <summary>
/// Returns the distinct internal followup staff that appear on the Pending Evaluation surface,
/// for the filter autocomplete. Sourced from appraisal.vw_AppraisalEvaluationList so the options
/// are scoped to the same universe the list filters over (no orphaned choices). Staff with no
/// resolvable name still surface with the username as the label so they remain selectable.
/// </summary>
public class GetPendingEvaluationStaffQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetPendingEvaluationStaffQuery, IReadOnlyList<InternalFollowupStaffOption>>
{
    public async Task<IReadOnlyList<InternalFollowupStaffOption>> Handle(
        GetPendingEvaluationStaffQuery query,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT DISTINCT
    InternalFollowupStaffId   AS Value,
    COALESCE(InternalFollowupStaffName, InternalFollowupStaffId) AS Label
FROM appraisal.vw_AppraisalEvaluationList
WHERE InternalFollowupStaffId IS NOT NULL
ORDER BY Label";

        var conn = connectionFactory.GetOpenConnection();
        var options = await conn.QueryAsync<InternalFollowupStaffOption>(sql);
        return options.ToList();
    }
}
