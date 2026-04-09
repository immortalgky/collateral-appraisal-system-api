using System.Text.Json;
using Workflow.Workflow.Models;

namespace Workflow.DocumentFollowups.Application;

/// <summary>
/// Helpers that inspect a <see cref="WorkflowInstance"/>'s embedded JSON definition to decide
/// whether the caller's activity has opted into the document-followup gate, and to resolve
/// the canonical activity name used to key PendingTask rows.
/// </summary>
internal static class ActivityFollowupHelpers
{
    public static bool ActivityCanRaiseFollowup(WorkflowInstance instance, string activityId)
    {
        var def = instance.WorkflowDefinition;
        if (def is null || string.IsNullOrEmpty(def.JsonDefinition)) return false;

        try
        {
            using var doc = JsonDocument.Parse(def.JsonDefinition);
            var root = doc.RootElement;
            if (root.TryGetProperty("workflowSchema", out var schema))
                root = schema;

            if (!root.TryGetProperty("activities", out var activities) ||
                activities.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var activity in activities.EnumerateArray())
            {
                if (!activity.TryGetProperty("id", out var idProp) || idProp.GetString() != activityId)
                    continue;

                if (!activity.TryGetProperty("properties", out var props) ||
                    props.ValueKind != JsonValueKind.Object)
                    return false;

                if (!props.TryGetProperty("canRaiseFollowup", out var flag))
                    return false;

                return flag.ValueKind == JsonValueKind.True ||
                       (flag.ValueKind == JsonValueKind.String &&
                        bool.TryParse(flag.GetString(), out var b) && b);
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    public static string? ResolveActivityName(WorkflowInstance instance, string activityId)
    {
        var def = instance.WorkflowDefinition;
        if (def is null || string.IsNullOrEmpty(def.JsonDefinition)) return null;

        try
        {
            using var doc = JsonDocument.Parse(def.JsonDefinition);
            var root = doc.RootElement;
            if (root.TryGetProperty("workflowSchema", out var schema))
                root = schema;

            if (!root.TryGetProperty("activities", out var activities))
                return null;

            foreach (var activity in activities.EnumerateArray())
            {
                if (!activity.TryGetProperty("id", out var idProp) || idProp.GetString() != activityId)
                    continue;
                if (activity.TryGetProperty("properties", out var props) &&
                    props.TryGetProperty("activityName", out var name))
                {
                    return name.GetString();
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }
}
