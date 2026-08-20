using Common.Application.Features.Monitoring.Shared;
using Dapper;
using Shared.CQRS;
using Shared.Data;

namespace Common.Application.Features.Monitoring.GetTaskTypes;

/// <summary>
/// Returns the task types actually present on a monitoring screen, for its taskType filter.
///
/// Sourced from common.vw_MonitoringPendingTasks — the same view the grid reads — so every option
/// is guaranteed to match at least one visible row, and its label matches the grid's Task Type
/// column (both are PendingTask.TaskDescription, i.e. the workflow activity's display name).
///
/// This deliberately does NOT read workflow.WorkflowDefinitions.JsonDefinition. That column holds
/// two different JSON shapes — the seeders store the whole config file (root "workflowSchema"),
/// while the Workflow Builder stores the bare WorkflowSchema — so parsing it dropped whole
/// workflows from the list depending only on how each definition happened to be saved.
/// </summary>
public class GetTaskTypesQueryHandler(
    ISqlConnectionFactory connectionFactory,
    MonitoringScopeService scopeService)
    : IQueryHandler<GetTaskTypesQuery, IReadOnlyList<TaskTypeOption>>
{
    public async Task<IReadOnlyList<TaskTypeOption>> Handle(
        GetTaskTypesQuery query,
        CancellationToken cancellationToken)
    {
        var monitoringType = MonitoringTypes.Normalize(query.MonitoringType);

        var scope = monitoringType == MonitoringTypes.External
            ? scopeService.ResolveExternalScope()
            : scopeService.ResolveInternalScope();

        var conditions = new List<string> { "MonitoringType = @MonitoringType", "TaskType IS NOT NULL" };
        var parameters = new DynamicParameters();
        parameters.Add("MonitoringType", monitoringType);

        // Same activity + team scope as the grid — a user may only filter by what they can see.
        // Empty scope ⇒ user holds no monitoring permission for this screen.
        if (!scopeService.TryBuildActivityFilter(scope, conditions, parameters))
            return [];

        // GROUP BY (not DISTINCT) so a task type whose rows carry differing descriptions still
        // yields exactly one option — the dropdown's value must be unique.
        // Column order MUST match TaskTypeOption's positional record constructor (Value, Label).
        var sql = $@"
SELECT
    TaskType                                                  AS Value,
    COALESCE(NULLIF(MIN(TaskDescription), ''), TaskType)      AS Label
FROM common.vw_MonitoringPendingTasks
WHERE {string.Join(" AND ", conditions)}
GROUP BY TaskType
ORDER BY Label";

        var conn = connectionFactory.GetOpenConnection();
        var options = await conn.QueryAsync<TaskTypeOption>(sql, parameters);

        return options.ToList();
    }
}
