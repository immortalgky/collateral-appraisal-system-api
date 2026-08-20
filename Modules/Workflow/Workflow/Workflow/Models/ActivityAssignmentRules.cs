using System.Text.Json;

namespace Workflow.Workflow.Models;

public record ActivityAssignmentRules(
    bool TeamConstrained,
    List<string> ExcludeAssigneesFrom)
{
    public static ActivityAssignmentRules Default => new(false, []);

    /// <summary>
    /// Reads the <c>assignmentRules</c> entry out of an activity's properties as parsed from the
    /// workflow definition JSON. Returns <see cref="Default"/> when the entry is absent or malformed.
    /// Shared by the assignment pipeline and the segregation-of-duties guard so the two cannot
    /// disagree about what a definition says.
    /// </summary>
    public static ActivityAssignmentRules Parse(
        IReadOnlyDictionary<string, object> properties,
        ILogger? logger = null,
        string? activityId = null)
    {
        if (!properties.TryGetValue("assignmentRules", out var rulesObj))
            return Default;

        try
        {
            if (rulesObj is JsonElement jsonElement)
            {
                var teamConstrained = false;
                var excludeFrom = new List<string>();

                if (jsonElement.TryGetProperty("teamConstrained", out var tc))
                    teamConstrained = tc.GetBoolean();

                if (jsonElement.TryGetProperty("excludeAssigneesFrom", out var ea) &&
                    ea.ValueKind == JsonValueKind.Array)
                    foreach (var item in ea.EnumerateArray())
                    {
                        var val = item.GetString();
                        if (!string.IsNullOrEmpty(val))
                            excludeFrom.Add(val);
                    }

                return new ActivityAssignmentRules(teamConstrained, excludeFrom);
            }

            if (rulesObj is Dictionary<string, object> dict)
            {
                var teamConstrained = dict.TryGetValue("teamConstrained", out var tc) && tc is true;
                var excludeFrom = new List<string>();

                if (dict.TryGetValue("excludeAssigneesFrom", out var ea) && ea is List<string> list)
                    excludeFrom = list;

                return new ActivityAssignmentRules(teamConstrained, excludeFrom);
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to parse assignmentRules for {ActivityId}", activityId);
        }

        return Default;
    }
}
