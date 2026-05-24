using System.Text.Json;
using Dapper;
using Shared.CQRS;
using Shared.Data;

namespace Common.Application.Features.Monitoring.GetTaskTypes;

/// <summary>
/// Returns the universe of all task types defined across active workflow definitions.
/// Reads JsonDefinition from workflow.WorkflowDefinitions, parses workflowSchema.activities[],
/// filters to entries whose type is one of the pending-task-producing kinds
/// (TaskActivity, FanOutTaskActivity, ApprovalActivity),
/// extracts name (label) and properties.activityName (value), dedupes by value, and sorts by label.
/// </summary>
public class GetTaskTypesQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetTaskTypesQuery, IReadOnlyList<TaskTypeOption>>
{
    private static readonly HashSet<string> PendingTaskProducingTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "TaskActivity",
        "FanOutTaskActivity",
        "ApprovalActivity"
    };

    public async Task<IReadOnlyList<TaskTypeOption>> Handle(
        GetTaskTypesQuery query,
        CancellationToken cancellationToken)
    {
        const string sql = "SELECT JsonDefinition FROM workflow.WorkflowDefinitions WHERE IsActive = 1";

        var conn = connectionFactory.GetOpenConnection();
        var jsonDefinitions = await conn.QueryAsync<string>(sql);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<TaskTypeOption>();

        foreach (var json in jsonDefinitions)
        {
            if (string.IsNullOrWhiteSpace(json)) continue;

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("workflowSchema", out var schema)) continue;
                if (!schema.TryGetProperty("activities", out var activities)) continue;

                foreach (var activity in activities.EnumerateArray())
                {
                    if (!activity.TryGetProperty("type", out var typeProp)) continue;
                    var type = typeProp.GetString() ?? string.Empty;

                    if (!PendingTaskProducingTypes.Contains(type)) continue;

                    if (!activity.TryGetProperty("properties", out var props)) continue;
                    if (!props.TryGetProperty("activityName", out var activityNameProp)) continue;

                    var value = activityNameProp.GetString();
                    if (string.IsNullOrWhiteSpace(value)) continue;

                    if (!seen.Add(value)) continue;

                    var label = activity.TryGetProperty("name", out var nameProp)
                        ? nameProp.GetString() ?? value
                        : value;

                    results.Add(new TaskTypeOption(value, label));
                }
            }
            catch (JsonException)
            {
                // Malformed definition — skip silently
            }
        }

        return results
            .OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
