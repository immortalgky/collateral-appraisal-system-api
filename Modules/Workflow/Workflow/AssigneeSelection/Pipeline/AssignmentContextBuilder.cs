using System.Text.Json;
using Workflow.AssigneeSelection.Teams;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;

namespace Workflow.AssigneeSelection.Pipeline;

public class AssignmentContextBuilder : IAssignmentContextBuilder
{
    private readonly ITeamService _teamService;
    private readonly ILogger<AssignmentContextBuilder> _logger;

    public AssignmentContextBuilder(ITeamService teamService, ILogger<AssignmentContextBuilder> logger)
    {
        _teamService = teamService;
        _logger = logger;
    }

    public async Task BuildAsync(AssignmentPipelineContext context, CancellationToken cancellationToken = default)
    {
        var activityCtx = context.ActivityContext;

        // 1. Parse assignmentRules from workflow definition JSON
        context.Rules = ParseAssignmentRules(activityCtx);

        // 2a. Read TeamId — check activity-specific teamIdVariable first
        var teamVarName = GetJsonString(activityCtx.Properties, "teamIdVariable");
        if (!string.IsNullOrEmpty(teamVarName))
        {
            var activityTeamId = GetJsonString(activityCtx.Variables, teamVarName);
            if (!string.IsNullOrEmpty(activityTeamId))
                context.TeamId = activityTeamId;
        }

        // 2b. If team-constrained but no explicit variable, derive from previous assignee's team
        if (string.IsNullOrEmpty(context.TeamId) && context.Rules.TeamConstrained)
        {
            var lastAssignee = GetMostRecentPriorAssignee(activityCtx);
            if (lastAssignee is not null)
            {
                var team = await _teamService.GetTeamForUserAsync(lastAssignee, cancellationToken);
                if (team is not null)
                {
                    context.TeamId = team.TeamId;
                    _logger.LogDebug(
                        "Derived TeamId={TeamId} from previous assignee {Assignee} for {ActivityId}",
                        team.TeamId, lastAssignee, activityCtx.ActivityId);
                }
            }
        }

        // 2c. Fall back to global TeamId variable
        if (string.IsNullOrEmpty(context.TeamId))
        {
            var globalTeamId = GetJsonString(activityCtx.Variables, "TeamId");
            if (!string.IsNullOrEmpty(globalTeamId))
                context.TeamId = globalTeamId;
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
    }

    /// <summary>
    /// Finds the most recent prior assignee by scanning completed activity executions
    /// in reverse completion order.
    /// </summary>
    internal static string? GetMostRecentPriorAssignee(ActivityContext activityCtx)
    {
        var executions = activityCtx.WorkflowInstance.ActivityExecutions;

        return executions
            .Where(e => e.Status == ActivityExecutionStatus.Completed && !string.IsNullOrEmpty(e.AssignedTo))
            .OrderByDescending(e => e.CompletedOn)
            .Select(e => e.AssignedTo)
            .FirstOrDefault();
    }

    private ActivityAssignmentRules ParseAssignmentRules(ActivityContext activityCtx)
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

    private static Dictionary<string, string> BuildPriorAssigneesMap(ActivityContext activityCtx)
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

    private static string? GetJsonString(Dictionary<string, object> dict, string key)
    {
        if (!dict.TryGetValue(key, out var val)) return null;
        if (val is string s) return s;
        if (val is JsonElement { ValueKind: JsonValueKind.String } je) return je.GetString();
        return val?.ToString();
    }
}
