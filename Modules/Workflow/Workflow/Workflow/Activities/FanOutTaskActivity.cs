using System.Text.Json;
using Workflow.AssigneeSelection.Pipeline;
using Workflow.Data.Repository;
using Workflow.Tasks.Models;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Events;
using Workflow.Workflow.Schema;
using Workflow.Workflow.Models;
using MassTransit;
using Shared.Messaging.Events;

namespace Workflow.Workflow.Activities;

/// <summary>
/// Creates N PendingTasks (one per invited company) when executed.
/// Transitions to the next activity only when ALL per-company tasks are terminal.
///
/// JSON activity definition properties:
///   fanOutVariable      : name of the workflow variable holding Guid[] of company IDs (e.g. "invitedCompanyIds")
///   assigneeGroup       : role group for each task (e.g. "ExtAdmin") — fallback when no stages[] present
///   activityName        : display name stored on each PendingTask row
///   dueAtVariable       : (optional) workflow variable name holding the DateTime? due date
///   initialStage        : (optional) name of the first stage; required when stages[] is present
///   stages[]            : (optional) per-stage group/actions definitions
///   onTimeout           : (optional) { "complete": "&lt;outcome&gt;" } applied per non-terminal item at cutoff
/// </summary>
public class FanOutTaskActivity(
    IAssignmentRepository assignmentRepository,
    IPublisher publisher,
    IPublishEndpoint publishEndpoint,
    IDateTimeProvider dateTimeProvider,
    IAssignmentPipeline assignmentPipeline,
    ILogger<FanOutTaskActivity> logger)
    : WorkflowActivityBase
{
    public override string ActivityType => ActivityTypes.FanOutTaskActivity;
    public override string Name => "Fan-Out Task Activity";
    public override string Description => "Spawns one task per company and gates transition until all are terminal";

    protected override async Task<ActivityResult> ExecuteActivityAsync(
        ActivityContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var fanOutVariable = GetProperty(context, "fanOutVariable", "invitedCompanyIds");
            var activityName = GetProperty(context, "activityName", context.ActivityId);

            // Resolve company IDs from workflow variables
            var companyIds = ResolveCompanyIds(context.Variables, fanOutVariable);

            if (companyIds.Count == 0)
            {
                logger.LogWarning(
                    "FanOutTaskActivity {ActivityId}: no company IDs found in variable '{FanOutVariable}'",
                    context.ActivityId, fanOutVariable);
                return ActivityResult.Failed($"Fan-out variable '{fanOutVariable}' is empty or missing");
            }

            // Resolve optional DueAt
            DateTime? dueAt = null;
            var dueAtVariable = GetProperty<string>(context, "dueAtVariable");
            if (!string.IsNullOrEmpty(dueAtVariable) &&
                context.Variables.TryGetValue(dueAtVariable, out var dueAtRaw))
                dueAt = ResolveDateTime(dueAtRaw);

            var correlationGuid = !string.IsNullOrEmpty(context.WorkflowInstance.CorrelationId)
                                  && Guid.TryParse(context.WorkflowInstance.CorrelationId, out var parsed)
                ? parsed
                : context.WorkflowInstance.Id;

            // --- Stage-aware path ---
            var stages = ParseStageDefinitions(context);
            if (stages.Count > 0)
            {
                var initialStageName = GetProperty(context, "initialStage", stages[0].Name);
                var initialStage = stages.First(s => s.Name == initialStageName);

                // Find the in-progress execution to record stage state
                var execution = FindActivityExecution(context);
                if (execution is null)
                {
                    // Should be impossible during ExecuteActivityAsync: the engine creates the
                    // ActivityExecution before invoking us. Log loud and proceed without per-item
                    // state — the legacy single-group path will still produce working PendingTasks,
                    // it just won't have stage history (so excludeAssigneesFrom-by-stage will miss).
                    logger.LogWarning(
                        "FanOutTaskActivity {ActivityId}: in-progress ActivityExecution not found; " +
                        "stage state will not be initialised for this fan-out spawn",
                        context.ActivityId);
                }

                var assignments = new List<FanOutCompanyAssignment>();

                foreach (var companyId in companyIds)
                {
                    assignments.Add(new FanOutCompanyAssignment(
                        companyId,
                        initialStage.AssigneeGroup,
                        activityName));

                    execution?.InitializeFanOutItem(
                        companyId,
                        initialStageName,
                        initialStage.AssigneeGroup,
                        assigneeUserId: null,
                        dateTimeProvider);
                }

                await publisher.Publish(
                    new FanOutTasksAssignedEvent(
                        correlationGuid,
                        activityName,
                        assignments,
                        dateTimeProvider.ApplicationNow,
                        context.WorkflowInstanceId,
                        context.ActivityId,
                        dueAt,
                        context.WorkflowInstance.StartedBy,
                        context.WorkflowInstance.Name),
                    cancellationToken);

                logger.LogInformation(
                    "FanOutTaskActivity {ActivityId}: spawned {Count} staged tasks (initialStage={Stage})",
                    context.ActivityId, companyIds.Count, initialStageName);
            }
            else
            {
                // --- Legacy single-group path ---
                var assigneeGroup = GetProperty(context, "assigneeGroup", "ExtAdmin");

                var assignments = companyIds
                    .Select(companyId => new FanOutCompanyAssignment(
                        companyId,
                        assigneeGroup,
                        activityName))
                    .ToList();

                await publisher.Publish(
                    new FanOutTasksAssignedEvent(
                        correlationGuid,
                        activityName,
                        assignments,
                        dateTimeProvider.ApplicationNow,
                        context.WorkflowInstanceId,
                        context.ActivityId,
                        dueAt,
                        context.WorkflowInstance.StartedBy,
                        context.WorkflowInstance.Name),
                    cancellationToken);

                logger.LogInformation(
                    "FanOutTaskActivity {ActivityId}: spawned {Count} tasks for companies [{CompanyIds}]",
                    context.ActivityId, companyIds.Count, string.Join(", ", companyIds));
            }

            return ActivityResult.Pending(new Dictionary<string, object>
            {
                ["fanOutCount"] = companyIds.Count
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "FanOutTaskActivity {ActivityId}: execution failed", context.ActivityId);
            return ActivityResult.Failed($"FanOutTaskActivity failed: {ex.Message}");
        }
    }

    protected override async Task<ActivityResult> ResumeActivityAsync(
        ActivityContext context,
        Dictionary<string, object> resumeInput,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Extract companyId from the resume input (set by the command handler)
            if (!resumeInput.TryGetValue("companyId", out var companyIdRaw) ||
                !Guid.TryParse(companyIdRaw?.ToString(), out var companyId))
            {
                logger.LogWarning(
                    "FanOutTaskActivity {ActivityId}: resume input missing companyId",
                    context.ActivityId);
                // Fall through — treat as non-company-scoped completion (admin/system)
            }
            else
            {
                // Archive this specific company's PendingTask
                var companyTask = await assignmentRepository.GetFanOutTaskByCompanyAsync(
                    context.WorkflowInstanceId, context.ActivityId, companyId, cancellationToken);

                if (companyTask is not null)
                {
                    var actionTaken = resumeInput.TryGetValue("decisionTaken", out var dt)
                        ? dt?.ToString() ?? "Completed"
                        : "Completed";
                    var completedBy = resumeInput.TryGetValue("completedBy", out var cb)
                        ? cb?.ToString()
                        : context.WorkflowInstance.CurrentAssignee;

                    var completedTask = CompletedTask.CreateFromPendingTask(
                        companyTask, actionTaken, dateTimeProvider.ApplicationNow);
                    await assignmentRepository.AddCompletedTaskAsync(completedTask, cancellationToken);
                    await assignmentRepository.RemovePendingTaskAsync(companyTask, cancellationToken);

                    // Record terminal on stage state (if stages are active)
                    var execution = FindActivityExecution(context);
                    execution?.CompleteFanOutItem(companyId, dateTimeProvider);

                    // Publish integration event for notifications
                    await publishEndpoint.Publish(new TaskCompletedIntegrationEvent
                    {
                        CorrelationId = companyTask.CorrelationId,
                        TaskName = companyTask.TaskName,
                        ActionTaken = actionTaken,
                        CompletedBy = completedBy ?? companyTask.AssignedTo,
                        WorkflowInstanceName = context.WorkflowInstance.Name,
                        Movement = "F"
                    }, cancellationToken);

                    logger.LogInformation(
                        "FanOutTaskActivity {ActivityId}: completed task for company {CompanyId}",
                        context.ActivityId, companyId);
                }
            }

            // Check if all fan-out tasks for this activity are now terminal (no PendingTasks left)
            var remaining = await assignmentRepository.GetFanOutPendingTasksAsync(
                context.WorkflowInstanceId, context.ActivityId, cancellationToken);

            if (remaining.Count > 0)
            {
                logger.LogInformation(
                    "FanOutTaskActivity {ActivityId}: {Remaining} tasks still pending — staying paused",
                    context.ActivityId, remaining.Count);

                // Still waiting — return Pending so engine does not transition
                return ActivityResult.Pending(new Dictionary<string, object>
                {
                    ["remainingCount"] = remaining.Count
                });
            }

            // All done — build output data and transition
            var decisionTaken = resumeInput.TryGetValue("decisionTaken", out var finalDt)
                ? finalDt?.ToString() ?? "Completed"
                : "Completed";

            var outputData = new Dictionary<string, object>
            {
                [$"{NormalizeActivityId(context.ActivityId)}_decisionTaken"] = decisionTaken,
                ["decision"] = "all_completed"
            };

            logger.LogInformation(
                "FanOutTaskActivity {ActivityId}: all tasks completed — transitioning",
                context.ActivityId);

            return ActivityResult.Success(outputData);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "FanOutTaskActivity {ActivityId}: resume failed", context.ActivityId);
            return ActivityResult.Failed($"FanOutTaskActivity resume failed: {ex.Message}");
        }
    }

    // ── Stage helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Parses the <c>stages[]</c> array from activity properties.
    /// Returns an empty list when the property is absent (legacy single-group mode).
    /// </summary>
    internal List<FanOutStageDefinition> ParseStageDefinitions(ActivityContext context)
    {
        if (!context.Properties.TryGetValue("stages", out var raw) || raw is null)
            return [];

        try
        {
            JsonElement je;
            if (raw is JsonElement element)
                je = element;
            else
            {
                var json = JsonSerializer.Serialize(raw);
                je = JsonSerializer.Deserialize<JsonElement>(json);
            }

            if (je.ValueKind != JsonValueKind.Array)
                return [];

            var stages = new List<FanOutStageDefinition>();
            foreach (var item in je.EnumerateArray())
            {
                var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                var group = item.TryGetProperty("assigneeGroup", out var g) ? g.GetString() ?? string.Empty : string.Empty;

                // Parse assignmentRules (optional)
                ActivityAssignmentRules? rules = null;
                if (item.TryGetProperty("assignmentRules", out var ar))
                {
                    var tc = ar.TryGetProperty("teamConstrained", out var tcProp) && tcProp.GetBoolean();
                    var excl = new List<string>();
                    if (ar.TryGetProperty("excludeAssigneesFrom", out var eaProp) &&
                        eaProp.ValueKind == JsonValueKind.Array)
                        foreach (var ea in eaProp.EnumerateArray())
                        {
                            var v = ea.GetString();
                            if (!string.IsNullOrEmpty(v)) excl.Add(v);
                        }
                    rules = new ActivityAssignmentRules(tc, excl);
                }

                // Parse optional per-stage strategy overrides. When absent, the stage inherits
                // initial/revisit strategies from the parent activity's properties dict.
                var initialStrategies = ReadStringArray(item, "initialAssignmentStrategies");
                var revisitStrategies = ReadStringArray(item, "revisitAssignmentStrategies");

                // Parse actions
                var actions = new List<FanOutStageAction>();
                if (item.TryGetProperty("actions", out var actArr) && actArr.ValueKind == JsonValueKind.Array)
                    foreach (var act in actArr.EnumerateArray())
                    {
                        var value = act.TryGetProperty("value", out var v) ? v.GetString() ?? string.Empty : string.Empty;
                        var label = act.TryGetProperty("label", out var l) ? l.GetString() : null;
                        var to = act.TryGetProperty("to", out var t) ? t.GetString() : null;
                        var complete = act.TryGetProperty("complete", out var c) ? c.GetString() : null;
                        actions.Add(new FanOutStageAction(value, label, to, complete));
                    }

                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(group))
                    stages.Add(new FanOutStageDefinition(name, group, rules, initialStrategies, revisitStrategies, actions));
            }

            return stages;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "FanOutTaskActivity {ActivityId}: failed to parse stages[]", context.ActivityId);
            return [];
        }
    }

    /// <summary>
    /// Builds a modified <see cref="ActivityContext"/> that has the given <paramref name="stage"/>'s
    /// <c>assigneeGroup</c> and <c>assignmentRules</c> injected into its properties dictionary,
    /// and carries the <paramref name="fanOutKey"/> so the assignment pipeline can resolve
    /// <c>excludeAssigneesFrom: ["&lt;activityId&gt;:&lt;stageName&gt;"]</c> entries.
    /// </summary>
    private static ActivityContext BuildStageContext(
        ActivityContext original,
        FanOutStageDefinition stage,
        Guid fanOutKey)
    {
        // Clone properties and override group + rules for this stage
        var props = new Dictionary<string, object>(original.Properties)
        {
            ["assigneeGroup"] = stage.AssigneeGroup
        };

        if (stage.AssignmentRules is not null)
            props["assignmentRules"] = JsonSerializer.SerializeToElement(new
            {
                teamConstrained = stage.AssignmentRules.TeamConstrained,
                excludeAssigneesFrom = stage.AssignmentRules.ExcludeAssigneesFrom
            });
        else
            props.Remove("assignmentRules");

        // Per-stage strategy overrides — null means "inherit from activity-level props".
        if (stage.InitialAssignmentStrategies is not null)
            props["initialAssignmentStrategies"] =
                JsonSerializer.SerializeToElement(stage.InitialAssignmentStrategies);

        if (stage.RevisitAssignmentStrategies is not null)
            props["revisitAssignmentStrategies"] =
                JsonSerializer.SerializeToElement(stage.RevisitAssignmentStrategies);

        return new ActivityContext
        {
            WorkflowInstanceId = original.WorkflowInstanceId,
            ActivityId = original.ActivityId,
            ActivityName = original.ActivityName,
            Properties = props,
            Variables = original.Variables,
            CurrentAssignee = original.CurrentAssignee,
            WorkflowInstance = original.WorkflowInstance,
            Movement = original.Movement,
            RuntimeOverrides = original.RuntimeOverrides,
            FanOutKey = fanOutKey
        };
    }

    /// <summary>
    /// Advances a fan-out item to the next stage: updates FanOutItemState, re-runs the
    /// assignment pipeline for the new stage's group/rules, and reassigns the PendingTask.
    /// Returns null when the task cannot be found.
    /// </summary>
    public async Task<PendingTask?> AdvanceFanOutItemStageAsync(
        ActivityContext context,
        Guid companyId,
        FanOutStageDefinition nextStage,
        string completedBy,
        CancellationToken cancellationToken)
    {
        var companyTask = await assignmentRepository.GetFanOutTaskByCompanyAsync(
            context.WorkflowInstanceId, context.ActivityId, companyId, cancellationToken);

        if (companyTask is null)
        {
            logger.LogWarning(
                "FanOutTaskActivity {ActivityId}: AdvanceStage — no PendingTask for company {CompanyId}",
                context.ActivityId, companyId);
            return null;
        }

        // 1) Close the outgoing stage FIRST so the pipeline's Tier 2 fan-out lookup
        //    (GetMostRecentFanOutStageCompletedBy) and ExclusionFilter (BuildPriorAssigneesMap)
        //    can read CompletedBy + ExitedOn from the just-closed history entry.
        var execution = FindActivityExecution(context);
        execution?.CloseCurrentStage(companyId, completedBy, dateTimeProvider);

        // 2) Re-run assignment pipeline with stage-specific context + fan-out key for exclusion lookup.
        var stageCtx = BuildStageContext(context, nextStage, companyId);
        var result = await assignmentPipeline.AssignAsync(stageCtx, cancellationToken);

        // Determine final AssignedTo + AssignedType.
        //   - Pool strategy sets Metadata["AssignedType"] = "2" (mirror of TaskActivity:127-131).
        //   - Round-robin / workload / etc. without metadata return a single user id → "1".
        //   - No assignee resolved → fall back to the stage's group pool ("2").
        // "1" = specific person, "2" = pool/group.
        string newAssignedTo;
        string newAssignedType;
        if (result.IsSuccess && !string.IsNullOrEmpty(result.AssigneeId))
        {
            newAssignedTo = result.AssigneeId;
            newAssignedType = result.Metadata?.TryGetValue("AssignedType", out var metaType) == true
                ? metaType?.ToString() ?? "1"
                : "1";
        }
        else
        {
            newAssignedTo = nextStage.AssigneeGroup;
            newAssignedType = "2";
        }

        var pickedSpecificUser = newAssignedType == "1";

        // 3) Open the new stage. AssignedTo carries the group/role label (matching the
        //    spawn convention from InitializeFanOutItem). AssigneeUserId is stamped only
        //    when a specific user was resolved, so audit/display can distinguish
        //    "assigned to a person" from "assigned to a pool".
        execution?.OpenNextStage(
            companyId,
            nextStage.Name,
            nextStage.AssigneeGroup,
            pickedSpecificUser ? newAssignedTo : null,
            dateTimeProvider);

        // Reassign the PendingTask (no archive/recreate — spec says update in-place)
        companyTask.Reassign(newAssignedTo, newAssignedType);

        logger.LogInformation(
            "FanOutTaskActivity {ActivityId}: advanced company {CompanyId} to stage {Stage}, assigned to {Assignee} (type={Type})",
            context.ActivityId, companyId, nextStage.Name, newAssignedTo, newAssignedType);

        return companyTask;
    }

    public override Task<Core.ValidationResult> ValidateAsync(
        ActivityContext context, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        var fanOutVariable = GetProperty<string>(context, "fanOutVariable");
        if (string.IsNullOrEmpty(fanOutVariable))
            errors.Add("FanOutTaskActivity: 'fanOutVariable' property is required");

        return Task.FromResult(errors.Any()
            ? Core.ValidationResult.Failure(errors.ToArray())
            : Core.ValidationResult.Success());
    }

    protected override WorkflowActivityExecution CreateActivityExecution(ActivityContext context)
    {
        return WorkflowActivityExecution.Create(
            context.WorkflowInstance.Id,
            context.ActivityId,
            Name,
            ActivityType,
            context.CurrentAssignee,
            context.Variables);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static List<Guid> ResolveCompanyIds(Dictionary<string, object> variables, string variableName)
    {
        if (!variables.TryGetValue(variableName, out var raw) || raw is null)
            return [];

        // Handle JsonElement array (most common after EF JSON round-trip)
        if (raw is JsonElement je && je.ValueKind == JsonValueKind.Array)
            return je.EnumerateArray()
                .Select(e => e.ValueKind == JsonValueKind.String
                    ? Guid.TryParse(e.GetString(), out var g) ? g : (Guid?)null
                    : (Guid?)null)
                .Where(g => g.HasValue)
                .Select(g => g!.Value)
                .ToList();

        // Handle List<object> or List<Guid>
        if (raw is IEnumerable<object> list)
            return list
                .Select(item => item switch
                {
                    Guid g => g,
                    string s when Guid.TryParse(s, out var g) => g,
                    JsonElement e when Guid.TryParse(e.GetString(), out var g) => g,
                    _ => (Guid?)null
                })
                .Where(g => g.HasValue)
                .Select(g => g!.Value)
                .ToList();

        // Handle Guid[]
        if (raw is Guid[] ids) return ids.ToList();

        return [];
    }

    private static DateTime? ResolveDateTime(object? raw)
    {
        return raw switch
        {
            DateTime dt => dt,
            string s when DateTime.TryParse(s, out var d) => d,
            JsonElement je when je.ValueKind == JsonValueKind.String
                                && DateTime.TryParse(je.GetString(), out var d) => d,
            _ => null
        };
    }

    /// <summary>
    /// Reads a string-array property from a JSON object element. Returns null when
    /// the property is absent or empty so callers can distinguish "unset → inherit"
    /// from "explicitly empty list".
    /// </summary>
    private static List<string>? ReadStringArray(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;

        var list = new List<string>();
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) continue;
            var s = item.GetString();
            if (!string.IsNullOrEmpty(s)) list.Add(s);
        }
        return list.Count > 0 ? list : null;
    }
}

// ── Stage definition models ───────────────────────────────────────────────────

/// <summary>
/// Parsed representation of one entry in <c>stages[]</c> from the activity JSON.
///
/// Per-stage overrides for <c>initialAssignmentStrategies</c> / <c>revisitAssignmentStrategies</c>
/// are optional. Unset (null) values inherit from the parent activity's properties dict — so
/// stages can opt-in to different strategies without restating the full inheritance chain.
/// </summary>
public record FanOutStageDefinition(
    string Name,
    string AssigneeGroup,
    ActivityAssignmentRules? AssignmentRules,
    List<string>? InitialAssignmentStrategies,
    List<string>? RevisitAssignmentStrategies,
    List<FanOutStageAction> Actions);

/// <summary>One action entry within a stage definition.</summary>
/// <param name="Value">Machine-readable action value (e.g. "SubmitToChecker").</param>
/// <param name="Label">Human-readable label (optional).</param>
/// <param name="To">Target stage name — present when action transitions to another stage.</param>
/// <param name="Complete">Terminal outcome string — present when action terminates the item.</param>
public record FanOutStageAction(string Value, string? Label, string? To, string? Complete);