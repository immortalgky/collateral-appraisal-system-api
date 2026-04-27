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

        // 2b. If team-constrained but no explicit variable, derive from previous assignee's team.
        //     Fan-out-aware path runs first: when we are inside a fan-out stage transition,
        //     the activity execution is still InProgress so GetMostRecentPriorAssignee (which only
        //     scans Completed executions) would miss the maker. We read CompletedBy from the
        //     outgoing stage's history entry instead.
        if (string.IsNullOrEmpty(context.TeamId) && context.Rules.TeamConstrained)
        {
            // Fan-out stage transition path (maker → checker, etc.)
            if (activityCtx.FanOutKey.HasValue)
            {
                var priorUser = GetMostRecentFanOutStageCompletedBy(activityCtx);
                if (!string.IsNullOrEmpty(priorUser))
                {
                    var team = await _teamService.GetTeamForUserAsync(priorUser, cancellationToken);
                    if (team is not null)
                    {
                        context.TeamId = team.TeamId;
                        _logger.LogDebug(
                            "Derived TeamId={TeamId} from fan-out stage CompletedBy={User} for {ActivityId}/{FanOutKey}",
                            team.TeamId, priorUser, activityCtx.ActivityId, activityCtx.FanOutKey);
                    }
                }
            }

            // Cross-activity path (executed when not a fan-out stage, or fan-out path yielded no team)
            if (string.IsNullOrEmpty(context.TeamId))
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

        // 4. Build PriorAssignees map from completed activity executions.
        //    When FanOutKey is set on the ActivityContext, also include per-item stage history
        //    entries keyed as "<activityId>:<stageName>" so excludeAssigneesFrom can reference
        //    prior stages on the same fan-out item.
        var fanOutKey = context.FanOutKey ?? activityCtx.FanOutKey;
        context.PriorAssignees = BuildPriorAssigneesMap(activityCtx, fanOutKey);

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
            .Where(e => e.Status == ActivityExecutionStatus.Completed && !string.IsNullOrEmpty(e.CompletedBy))
            .OrderByDescending(e => e.CompletedOn)
            .Select(e => e.CompletedBy)
            .FirstOrDefault();
    }

    /// <summary>
    /// For a fan-out stage transition (e.g. maker → checker), finds the <c>CompletedBy</c>
    /// value stamped on the most recently exited stage for this fan-out key.
    /// The activity execution is still <c>InProgress</c> during a stage transition, so
    /// <see cref="GetMostRecentPriorAssignee"/> (which only scans Completed executions) cannot
    /// see it. This helper reads directly from <see cref="FanOutItemState.History"/>.
    /// Returns <c>null</c> when <c>FanOutKey</c> is absent, the item is not found, or no
    /// completed stage history entry exists yet (e.g. first stage of a new fan-out).
    /// </summary>
    internal static string? GetMostRecentFanOutStageCompletedBy(ActivityContext activityCtx)
    {
        if (!activityCtx.FanOutKey.HasValue) return null;

        // Locate the in-flight execution for this activity (Status != Completed).
        var execution = activityCtx.WorkflowInstance.ActivityExecutions
            .FirstOrDefault(e => e.ActivityId == activityCtx.ActivityId
                                 && e.Status != ActivityExecutionStatus.Completed);
        if (execution is null) return null;

        var item = execution.FanOutItems.FirstOrDefault(i => i.FanOutKey == activityCtx.FanOutKey.Value);
        if (item is null) return null;

        return item.History
            .Where(h => !string.IsNullOrEmpty(h.CompletedBy) && h.ExitedOn.HasValue)
            .OrderByDescending(h => h.ExitedOn)
            .Select(h => h.CompletedBy)
            .FirstOrDefault();
    }

    private ActivityAssignmentRules ParseAssignmentRules(ActivityContext activityCtx)
    {
        // Try to get assignmentRules from activity properties (parsed from JSON definition)
        if (activityCtx.Properties.TryGetValue("assignmentRules", out var rulesObj))
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
                _logger.LogWarning(ex, "Failed to parse assignmentRules for {ActivityId}", activityCtx.ActivityId);
            }

        return ActivityAssignmentRules.Default;
    }

    /// <summary>
    /// Builds the prior-assignees map used by <see cref="ExclusionFilter"/>.
    /// Keys are either bare activity ids or <c>&lt;activityId&gt;:&lt;stageName&gt;</c> entries.
    ///
    /// When <paramref name="fanOutKey"/> is provided and an <c>excludeAssigneesFrom</c> entry
    /// contains a colon, the lookup reads from
    /// <see cref="WorkflowActivityExecution.FanOutItems"/> filtered by the fan-out key and
    /// stage name, returning the first <see cref="StageAssignment.CompletedBy"/> found.
    /// <c>CompletedBy</c> is the actual user who completed the stage — the same identifier
    /// shape <c>excludeAssigneesFrom</c> wants to compare against. <c>AssigneeUserId</c> is
    /// not used here because it is only set for stages whose pipeline picked a specific user
    /// and is null for the initial fan-out spawn (where the maker is assigned to a group pool).
    /// Bare activity-id entries keep the existing completed-execution lookup.
    /// </summary>
    private static Dictionary<string, string> BuildPriorAssigneesMap(
        ActivityContext activityCtx,
        Guid? fanOutKey)
    {
        var map = new Dictionary<string, string>();
        var executions = activityCtx.WorkflowInstance.ActivityExecutions;

        // --- Bare activity-id entries (original behavior) ---
        foreach (var exec in executions.Where(e =>
                     e.Status == ActivityExecutionStatus.Completed && !string.IsNullOrEmpty(e.AssignedTo)))
            map[exec.ActivityId] = exec.AssignedTo!;

        // --- <activityId>:<stageName> entries (stage-scoped history) ---
        if (fanOutKey.HasValue)
            foreach (var exec in executions)
            {
                foreach (var item in exec.FanOutItems.Where(i => i.FanOutKey == fanOutKey.Value))
                    foreach (var history in item.History.Where(h => !string.IsNullOrEmpty(h.CompletedBy)))
                    {
                        var stageKey = $"{exec.ActivityId}:{history.StageName}";
                        // First entry wins — earliest completion per stage for this fan-out key
                        map.TryAdd(stageKey, history.CompletedBy!);
                    }
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