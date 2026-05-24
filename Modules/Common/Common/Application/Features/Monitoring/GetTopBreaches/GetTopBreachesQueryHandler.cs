using Common.Application.Features.Monitoring.Shared;
using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Identity;

namespace Common.Application.Features.Monitoring.GetTopBreaches;

/// <summary>
/// UNION ALL across the three OLA sections of common.vw_MonitoringPendingTasks,
/// restricted to rows where OlaVarianceHours &gt; 0 (actively breached), ordered by
/// OlaVarianceHours DESC, and limited to the requested top-N count.
///
/// Server-side permission filter:
///   Internal rows  — included when user holds any MONITORING:PENDING_INTERNAL:* permission.
///   External rows  — included when user holds any MONITORING:PENDING_EXTERNAL:* permission.
///   Followup rows  — included when user holds MONITORING:PENDING_FOLLOWUP.
/// </summary>
public class GetTopBreachesQueryHandler(
    ISqlConnectionFactory connectionFactory,
    MonitoringScopeService scopeService,
    ICurrentUserService currentUserService)
    : IQueryHandler<GetTopBreachesQuery, IReadOnlyList<TopBreachDto>>
{
    public async Task<IReadOnlyList<TopBreachDto>> Handle(
        GetTopBreachesQuery query,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(query.Limit, 1, 50);

        var internalActivityIds = scopeService.GetInternalActivityIds();
        var externalActivityIds = scopeService.GetExternalActivityIds();
        var hasFollowup = currentUserService.HasAnyPermission(MonitoringPermissions.PendingFollowup);

        // Build UNION ALL branches only for sections the user is permitted to see.
        var branches = new List<string>();
        var parameters = new DynamicParameters();

        if (internalActivityIds.Length > 0)
        {
            parameters.Add("InternalActivityIds", internalActivityIds);
            branches.Add(@"
    SELECT
        AppraisalId,
        AppraisalNumber,
        CustomerName,
        'pending-internal' AS SectionId,
        OlaVarianceHours,
        TaskType
    FROM common.vw_MonitoringPendingTasks
    WHERE MonitoringType = 'Internal'
      AND ActivityId IN @InternalActivityIds
      AND OlaVarianceHours > 0");
        }

        if (externalActivityIds.Length > 0)
        {
            parameters.Add("ExternalActivityIds", externalActivityIds);
            branches.Add(@"
    SELECT
        AppraisalId,
        AppraisalNumber,
        CustomerName,
        'pending-external' AS SectionId,
        OlaVarianceHours,
        TaskType
    FROM common.vw_MonitoringPendingTasks
    WHERE MonitoringType = 'External'
      AND ActivityId IN @ExternalActivityIds
      AND OlaVarianceHours > 0");
        }

        if (hasFollowup)
        {
            parameters.Add("FollowupActivityIds", MonitoringActivityMap.Followup);
            branches.Add(@"
    SELECT
        AppraisalId,
        AppraisalNumber,
        CustomerName,
        'pending-followup' AS SectionId,
        OlaVarianceHours,
        TaskType
    FROM common.vw_MonitoringPendingTasks
    WHERE ActivityId IN @FollowupActivityIds
      AND OlaVarianceHours > 0");
        }

        if (branches.Count == 0)
            return [];

        parameters.Add("Limit", limit);

        var sql = $@"
SELECT TOP (@Limit)
    AppraisalId,
    AppraisalNumber,
    CustomerName,
    SectionId,
    OlaVarianceHours,
    TaskType
FROM (
{string.Join(@"
    UNION ALL
", branches)}
) AS Combined
ORDER BY OlaVarianceHours DESC";

        var conn = connectionFactory.GetOpenConnection();
        var rows = await conn.QueryAsync<TopBreachDto>(sql, parameters);
        return rows.AsList();
    }
}
