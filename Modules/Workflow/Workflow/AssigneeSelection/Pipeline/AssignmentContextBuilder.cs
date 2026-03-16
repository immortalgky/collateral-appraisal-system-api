using System.Text.Json;
using Workflow.Workflow.Models;

namespace Workflow.AssigneeSelection.Pipeline;

public class AssignmentContextBuilder : IAssignmentContextBuilder
{
    private readonly ILogger<AssignmentContextBuilder> _logger;

    public AssignmentContextBuilder(ILogger<AssignmentContextBuilder> logger)
    {
        _logger = logger;
    }

    public Task BuildAsync(AssignmentPipelineContext context, CancellationToken cancellationToken = default)
    {
        var activityCtx = context.ActivityContext;

        // 1. Parse assignmentRules from workflow definition JSON
        context.Rules = ParseAssignmentRules(activityCtx);

        // 2. Read TeamId from workflow variables
        if (activityCtx.Variables.TryGetValue("TeamId", out var teamIdObj) && teamIdObj is string teamId)
        {
            context.TeamId = teamId;
        }

        // 3. Extract RuntimeOverride for this activity
        context.RuntimeOverride = activityCtx.RuntimeOverrides;

        // 4. Build PriorAssignees map from completed activity executions
        context.PriorAssignees = BuildPriorAssigneesMap(activityCtx);

        _logger.LogDebug(
            "Context built for {ActivityId}: TeamConstrained={TeamConstrained}, TeamId={TeamId}, ExcludeFrom=[{ExcludeFrom}], PriorAssignees={Count}",
            activityCtx.ActivityId,
            context.Rules.TeamConstrained,
            context.TeamId,
            string.Join(",", context.Rules.ExcludeAssigneesFrom),
            context.PriorAssignees.Count);

        return Task.CompletedTask;
    }

    private ActivityAssignmentRules ParseAssignmentRules(Workflow.Activities.Core.ActivityContext activityCtx)
    {
        // Try to get assignmentRules from activity properties (parsed from JSON definition)
        if (activityCtx.Properties.TryGetValue("assignmentRules", out var rulesObj))
        {
            try
            {
                if (rulesObj is JsonElement jsonElement)
                {
                    var teamConstrained = false;
                    var excludeFrom = new List<string>();

                    if (jsonElement.TryGetProperty("teamConstrained", out var tc))
                        teamConstrained = tc.GetBoolean();

                    if (jsonElement.TryGetProperty("excludeAssigneesFrom", out var ea) && ea.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in ea.EnumerateArray())
                        {
                            var val = item.GetString();
                            if (!string.IsNullOrEmpty(val))
                                excludeFrom.Add(val);
                        }
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
                _logger.LogWarning(ex, "Failed to parse assignmentRules for {ActivityId}", activityCtx.ActivityId);
            }
        }

        return ActivityAssignmentRules.Default;
    }

    private static Dictionary<string, string> BuildPriorAssigneesMap(Workflow.Activities.Core.ActivityContext activityCtx)
    {
        var map = new Dictionary<string, string>();
        var executions = activityCtx.WorkflowInstance.ActivityExecutions;

        foreach (var exec in executions.Where(e => e.Status == ActivityExecutionStatus.Completed && !string.IsNullOrEmpty(e.AssignedTo)))
        {
            // Last completed assignee per activity wins
            map[exec.ActivityId] = exec.AssignedTo!;
        }

        return map;
    }
}
