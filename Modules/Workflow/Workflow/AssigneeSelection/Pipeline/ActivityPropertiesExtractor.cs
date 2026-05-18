using System.Text.Json;
using Microsoft.Extensions.Logging;
using Workflow.Workflow.Models;

namespace Workflow.AssigneeSelection.Pipeline;

/// <summary>
/// Shared helper that extracts the properties and assignmentRules for a specific
/// activity from a WorkflowInstance's JSON definition.
/// Centralised here to avoid duplication between GetEligibleAssigneesQueryHandler
/// and ReassignTaskCommandHandler.
/// </summary>
public static class ActivityPropertiesExtractor
{
    /// <summary>
    /// Parses the workflow definition JSON and returns properties for the requested activity.
    /// Returns an empty dictionary when the definition is absent, the activity is not found,
    /// or JSON parsing fails. Parse failures are logged at Warning level when a logger is supplied.
    /// </summary>
    public static Dictionary<string, object> Extract(
        WorkflowInstance instance,
        string activityId,
        ILogger? logger = null)
    {
        var definition = instance.WorkflowDefinition;
        if (definition is null || string.IsNullOrEmpty(definition.JsonDefinition))
            return new Dictionary<string, object>();

        try
        {
            using var doc = JsonDocument.Parse(definition.JsonDefinition);
            var root = doc.RootElement;

            if (!root.TryGetProperty("activities", out var activities) ||
                activities.ValueKind != JsonValueKind.Array)
                return new Dictionary<string, object>();

            foreach (var activity in activities.EnumerateArray())
            {
                if (activity.TryGetProperty("id", out var idProp) && idProp.GetString() == activityId)
                {
                    var props = new Dictionary<string, object>();

                    if (activity.TryGetProperty("properties", out var propElement))
                    {
                        foreach (var p in propElement.EnumerateObject())
                            props[p.Name] = p.Value.Clone();
                    }

                    if (activity.TryGetProperty("assignmentRules", out var rulesElement))
                        props["assignmentRules"] = rulesElement.Clone();

                    return props;
                }
            }
        }
        catch (JsonException ex)
        {
            logger?.LogWarning(ex,
                "Failed to parse workflow definition JSON for WorkflowInstanceId {WorkflowInstanceId}, ActivityId {ActivityId}",
                instance.Id, activityId);
        }

        return new Dictionary<string, object>();
    }
}
