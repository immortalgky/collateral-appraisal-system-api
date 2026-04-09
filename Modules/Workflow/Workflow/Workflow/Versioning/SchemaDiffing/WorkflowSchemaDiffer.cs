using Workflow.Workflow.Schema;
using DomainBreakingChange = Workflow.Workflow.Models.BreakingChange;
using ChangeImpact = Workflow.Workflow.Models.ChangeImpact;

namespace Workflow.Workflow.Versioning.SchemaDiffing;

/// <summary>
/// Default IWorkflowSchemaDiffer implementation.
/// Diff rules (MVP, no rename detection — id changes = remove + add):
///   1. Activity removed (id in old but not new)
///   2. Activity type changed (same id, different Type)
///   3. Required property added (same id, new property with Required=true and no default)
///   4. Transition removed from a source activity that still exists
/// Ignored: display metadata, positions, pure additions.
/// </summary>
public class WorkflowSchemaDiffer : IWorkflowSchemaDiffer
{
    public IReadOnlyList<DomainBreakingChange> Diff(WorkflowSchema oldSchema, WorkflowSchema newSchema)
    {
        if (oldSchema is null) throw new ArgumentNullException(nameof(oldSchema));
        if (newSchema is null) throw new ArgumentNullException(nameof(newSchema));

        var changes = new List<DomainBreakingChange>();

        var oldActivities = oldSchema.Activities.ToDictionary(a => a.Id);
        var newActivities = newSchema.Activities.ToDictionary(a => a.Id);

        // 1. Activity removed
        foreach (var oldActivity in oldActivities.Values)
        {
            if (!newActivities.ContainsKey(oldActivity.Id))
            {
                changes.Add(DomainBreakingChange.ActivityRemoved(
                    oldActivity.Id,
                    $"Activity '{oldActivity.Name}' ({oldActivity.Id}) was removed"));
            }
        }

        // 2. Activity type changed & 3. Required property added (no default)
        foreach (var (id, newActivity) in newActivities)
        {
            if (!oldActivities.TryGetValue(id, out var oldActivity))
                continue; // pure addition — ignored

            if (!string.Equals(oldActivity.Type, newActivity.Type, StringComparison.Ordinal))
            {
                changes.Add(DomainBreakingChange.PropertyChanged(
                    id,
                    "Type",
                    $"Activity '{id}' type changed from '{oldActivity.Type}' to '{newActivity.Type}'",
                    oldActivity.Type,
                    newActivity.Type));
            }

            // Required property additions.
            // Semantics: a property appears in new.Properties with a value that signals required/no-default, and is absent in old.
            foreach (var (propName, propValue) in newActivity.Properties)
            {
                if (oldActivity.Properties.ContainsKey(propName)) continue;
                if (!IsRequiredWithNoDefault(propValue)) continue;

                changes.Add(DomainBreakingChange.PropertyChanged(
                    id,
                    propName,
                    $"Activity '{id}' gained required property '{propName}' with no default",
                    null,
                    "required"));
            }
        }

        // 4. Transition removed from a source that still exists in the new schema.
        // Match transitions by (From, To, Condition).
        var newTransitionKeys = new HashSet<string>(
            newSchema.Transitions.Select(TransitionKey));

        foreach (var oldTransition in oldSchema.Transitions)
        {
            var key = TransitionKey(oldTransition);
            if (newTransitionKeys.Contains(key))
                continue;

            // Only flag if the source activity exists in both schemas — otherwise the
            // activity-removed change already covers the impact.
            if (!newActivities.ContainsKey(oldTransition.From)) continue;
            if (!oldActivities.ContainsKey(oldTransition.From)) continue;

            changes.Add(new DomainBreakingChange
            {
                Type = "TransitionRemoved",
                Description =
                    $"Transition from '{oldTransition.From}' to '{oldTransition.To}' was removed",
                AffectedComponent = oldTransition.From,
                Impact = ChangeImpact.High,
                MigrationData = new Dictionary<string, object>
                {
                    ["From"] = oldTransition.From,
                    ["To"] = oldTransition.To,
                    ["Condition"] = oldTransition.Condition ?? string.Empty
                }
            });
        }

        return changes;
    }

    private static string TransitionKey(TransitionDefinition t)
        => $"{t.From}->{t.To}|{t.Condition ?? string.Empty}";

    /// <summary>
    /// Heuristic: a property value is "required with no default" if it is represented as a descriptor
    /// object containing { required: true } AND no "default"/"defaultValue" key, OR if the raw value is null.
    /// Kept intentionally conservative so non-descriptor values (actual runtime data) aren't misclassified.
    /// </summary>
    private static bool IsRequiredWithNoDefault(object? value)
    {
        if (value is null) return false;

        if (value is IDictionary<string, object> dict)
        {
            var required = dict.TryGetValue("required", out var r) && r is bool b && b;
            if (!required) return false;

            var hasDefault = dict.ContainsKey("default") || dict.ContainsKey("defaultValue");
            return !hasDefault;
        }

        // System.Text.Json paths commonly return JsonElement — best-effort check.
        var type = value.GetType();
        if (type.Name == "JsonElement")
        {
            var getProp = type.GetMethod("TryGetProperty", new[] { typeof(string), type.MakeByRefType() });
            if (getProp is null) return false;

            var args = new object?[] { "required", null };
            if (getProp.Invoke(value, args) is not true) return false;
            var requiredElem = args[1]!;
            var getBool = requiredElem.GetType().GetMethod("GetBoolean");
            var requiredValue = getBool is not null && (bool)getBool.Invoke(requiredElem, null)!;
            if (!requiredValue) return false;

            var defArgs = new object?[] { "default", null };
            var hasDefault = (bool)getProp.Invoke(value, defArgs)!;
            var defaultValueArgs = new object?[] { "defaultValue", null };
            hasDefault |= (bool)getProp.Invoke(value, defaultValueArgs)!;
            return !hasDefault;
        }

        return false;
    }
}
