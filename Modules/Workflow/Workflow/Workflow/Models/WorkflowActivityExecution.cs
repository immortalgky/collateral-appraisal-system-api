using Shared.DDD;

namespace Workflow.Workflow.Models;

public class WorkflowActivityExecution : Entity<Guid>
{
    public Guid WorkflowInstanceId { get; private set; }
    public string ActivityId { get; private set; } = default!;
    public string ActivityName { get; private set; } = default!;
    public string ActivityType { get; private set; } = default!;
    public ActivityExecutionStatus Status { get; private set; }
    public string? AssignedTo { get; private set; }
    public DateTime StartedOn { get; private set; }
    public DateTime? CompletedOn { get; private set; }
    public string? CompletedBy { get; private set; }
    public Dictionary<string, object> InputData { get; private set; } = new();
    public Dictionary<string, object> OutputData { get; private set; } = new();
    public string? ErrorMessage { get; private set; }
    public string? Comments { get; private set; }

    /// <summary>
    /// Direction stamp of the action that completed this activity. "F" (forward, default)
    /// or "B" (backward / route-back). Read by the engine when building the NEXT activity's
    /// ActivityContext so its PendingTask inherits the same movement.
    /// </summary>
    public string Movement { get; private set; } = "F";

    /// <summary>
    /// Per-fan-out-item stage state. Populated only for FanOutTaskActivity instances that
    /// declare a <c>stages[]</c> definition. Empty list means legacy single-group behavior.
    /// </summary>
    public List<FanOutItemState> FanOutItems { get; private set; } = new();

    public WorkflowInstance WorkflowInstance { get; private set; } = default!;

    private WorkflowActivityExecution()
    {
        // For EF Core
    }

    public static WorkflowActivityExecution Create(
        Guid workflowInstanceId,
        string activityId,
        string activityName,
        string activityType,
        string? assignedTo = null,
        Dictionary<string, object>? inputData = null)
    {
        return new WorkflowActivityExecution
        {
            //Id = Guid.NewGuid(),
            WorkflowInstanceId = workflowInstanceId,
            ActivityId = activityId,
            ActivityName = activityName,
            ActivityType = activityType,
            Status = ActivityExecutionStatus.Pending,
            AssignedTo = assignedTo,
            StartedOn = DateTime.Now,
            InputData = inputData ?? new Dictionary<string, object>()
        };
    }

    public void Start()
    {
        Status = ActivityExecutionStatus.InProgress;
        StartedOn = DateTime.Now;
    }

    public void Complete(
        string completedBy,
        Dictionary<string, object>? outputData = null,
        string? comments = null)
    {
        Status = ActivityExecutionStatus.Completed;
        CompletedOn = DateTime.Now;
        CompletedBy = completedBy;
        OutputData = outputData ?? new Dictionary<string, object>();
        Comments = comments;
    }

    public void Fail(string errorMessage)
    {
        Status = ActivityExecutionStatus.Failed;
        CompletedOn = DateTime.Now;
        ErrorMessage = errorMessage;
    }

    public void Skip(string reason)
    {
        Status = ActivityExecutionStatus.Skipped;
        CompletedOn = DateTime.Now;
        Comments = reason;
    }

    public void Cancel(string reason)
    {
        Status = ActivityExecutionStatus.Cancelled;
        CompletedOn = DateTime.Now;
        Comments = reason;
    }

    public void UpdateAssignee(string? assigneeId)
    {
        AssignedTo = assigneeId;
    }

    public void StampMovement(string movement)
    {
        Movement = string.IsNullOrWhiteSpace(movement) ? "F" : movement;
    }

    // ── Fan-out stage methods ─────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="FanOutItemState"/> entry for the given fan-out item and records
    /// the opening <see cref="StageAssignment"/> history entry for <paramref name="initialStage"/>.
    /// No-op if an entry for this key already exists (idempotent).
    /// </summary>
    public void InitializeFanOutItem(
        Guid fanOutKey,
        string initialStage,
        string assignedTo,
        string? assigneeUserId,
        IDateTimeProvider dateTimeProvider)
    {
        if (FanOutItems.Any(i => i.FanOutKey == fanOutKey))
            return;

        var item = new FanOutItemState
        {
            FanOutKey = fanOutKey,
            CurrentStage = initialStage
        };
        item.History.Add(new StageAssignment
        {
            StageName = initialStage,
            AssignedTo = assignedTo,
            AssigneeUserId = assigneeUserId,
            EnteredOn = dateTimeProvider.ApplicationNow
        });
        FanOutItems.Add(item);
    }

    /// <summary>
    /// Closes the current open stage history entry: stamps <c>ExitedOn</c> and
    /// <paramref name="completedBy"/>. Idempotent — no-op if no open entry exists.
    /// Call this BEFORE running the assignment pipeline for the next stage so the
    /// pipeline's Tier 2 fan-out lookup can read <c>CompletedBy</c>.
    /// </summary>
    /// <param name="completedBy">
    /// Username of the actor that completed the outgoing stage. Required — must match the
    /// identifier shape <c>ITeamService.GetTeamForUserAsync</c> resolves (i.e. what
    /// <see cref="CompletedBy"/> stores). System-driven callers (timeouts, scheduled transitions)
    /// MUST pass an explicit sentinel (e.g. <c>"SYSTEM"</c>) rather than null, accepting that
    /// Tier 2 team derivation will fall through to the cross-activity / global path.
    /// </param>
    public void CloseCurrentStage(
        Guid fanOutKey,
        string completedBy,
        IDateTimeProvider dateTimeProvider)
    {
        if (string.IsNullOrEmpty(completedBy))
            throw new ArgumentException(
                "completedBy is required so the next stage's assignment pipeline can derive a team. " +
                "System callers should pass an explicit sentinel like \"SYSTEM\".",
                nameof(completedBy));

        var item = FanOutItems.FirstOrDefault(i => i.FanOutKey == fanOutKey)
                   ?? throw new InvalidOperationException(
                       $"FanOutItemState not found for key {fanOutKey} on activity {ActivityId}");

        var current = item.History.LastOrDefault(h => h.ExitedOn is null);
        if (current is null) return;

        current.ExitedOn = dateTimeProvider.ApplicationNow;
        current.CompletedBy = completedBy;
    }

    /// <summary>
    /// Opens a new stage history entry and updates <see cref="FanOutItemState.CurrentStage"/>.
    /// Call this AFTER the assignment pipeline has resolved an assignee for the next stage,
    /// so the new entry carries the resolved <paramref name="assignedTo"/> and
    /// <paramref name="assigneeUserId"/>.
    /// </summary>
    public void OpenNextStage(
        Guid fanOutKey,
        string nextStage,
        string assignedTo,
        string? assigneeUserId,
        IDateTimeProvider dateTimeProvider)
    {
        if (string.IsNullOrEmpty(nextStage))
            throw new ArgumentException("nextStage is required.", nameof(nextStage));
        if (string.IsNullOrEmpty(assignedTo))
            throw new ArgumentException(
                "assignedTo is required (the group/role label for the new stage).",
                nameof(assignedTo));

        var item = FanOutItems.FirstOrDefault(i => i.FanOutKey == fanOutKey)
                   ?? throw new InvalidOperationException(
                       $"FanOutItemState not found for key {fanOutKey} on activity {ActivityId}");

        item.CurrentStage = nextStage;
        item.History.Add(new StageAssignment
        {
            StageName = nextStage,
            AssignedTo = assignedTo,
            AssigneeUserId = assigneeUserId,
            EnteredOn = dateTimeProvider.ApplicationNow
        });
    }

    /// <summary>
    /// Convenience wrapper for callers that don't need to interleave a pipeline call between
    /// closing the outgoing stage and opening the next one. For fan-out stage transitions that
    /// re-run the assignment pipeline, call <see cref="CloseCurrentStage"/> + pipeline +
    /// <see cref="OpenNextStage"/> separately so Tier 2 team derivation can see CompletedBy.
    /// </summary>
    /// <remarks>
    /// Throws <see cref="ArgumentException"/> when <paramref name="completedBy"/> is null/empty
    /// (delegated from <see cref="CloseCurrentStage"/>). System callers must pass an explicit
    /// sentinel like <c>"SYSTEM"</c>.
    /// </remarks>
    public void AdvanceStage(
        Guid fanOutKey,
        string nextStage,
        string assignedTo,
        string? assigneeUserId,
        IDateTimeProvider dateTimeProvider,
        string completedBy)
    {
        CloseCurrentStage(fanOutKey, completedBy, dateTimeProvider);
        OpenNextStage(fanOutKey, nextStage, assignedTo, assigneeUserId, dateTimeProvider);
    }

    /// <summary>
    /// Closes the current stage's open history entry, marking it terminal.
    /// Does NOT change <see cref="FanOutItemState.CurrentStage"/> — callers use the final
    /// outcome string for their own bookkeeping.
    /// </summary>
    public void CompleteFanOutItem(Guid fanOutKey, IDateTimeProvider dateTimeProvider)
    {
        var item = FanOutItems.FirstOrDefault(i => i.FanOutKey == fanOutKey);
        if (item is null) return;

        var current = item.History.LastOrDefault(h => h.ExitedOn is null);
        if (current is not null)
            current.ExitedOn = dateTimeProvider.ApplicationNow;
    }
}

/// <summary>
/// Per-company stage state stored as a JSON column on
/// <see cref="WorkflowActivityExecution.FanOutItems"/>.
/// </summary>
public class FanOutItemState
{
    /// <summary>Company Id for this fan-out slot.</summary>
    public Guid FanOutKey { get; set; }

    /// <summary>Name of the current active stage (e.g. "maker", "checker").</summary>
    public string CurrentStage { get; set; } = default!;

    /// <summary>Ordered record of stage assignments with enter/exit timestamps.</summary>
    public List<StageAssignment> History { get; set; } = new();
}

/// <summary>
/// One entry per stage visit in <see cref="FanOutItemState.History"/>.
///
/// IMPORTANT: <see cref="AssignedTo"/> on this type is the GROUP/ROLE LABEL by convention
/// (matching the spawn — see <see cref="WorkflowActivityExecution.InitializeFanOutItem"/>).
/// This is DIFFERENT from <c>PendingTask.AssignedTo</c>, which carries either a username
/// (when AssignedType="1") or a group label (when AssignedType="2"). Do not assume the two
/// fields are interchangeable. The actor (who actually completed the stage) lives in
/// <see cref="CompletedBy"/>; the resolved user (when the pipeline picked one specifically)
/// lives in <see cref="AssigneeUserId"/>.
/// </summary>
public class StageAssignment
{
    public string StageName { get; set; } = default!;
    /// <summary>
    /// Resolved user id when the pipeline picked a specific person for this stage; null
    /// when the stage is assigned to a pool. Not a reliable "who acted" — for that, use
    /// <see cref="CompletedBy"/>.
    /// </summary>
    public string? AssigneeUserId { get; set; }
    /// <summary>
    /// The group/role label assigned to this stage (e.g. "ExtAdmin", "ExtAppraisalChecker").
    /// Always carries the group label, regardless of whether the pipeline ultimately
    /// resolved a specific user. Diverges from <c>PendingTask.AssignedTo</c> by design.
    /// </summary>
    public string AssignedTo { get; set; } = default!;
    public DateTime EnteredOn { get; set; }
    public DateTime? ExitedOn { get; set; }
    /// <summary>
    /// The actual user who completed this stage (stamped when the stage exits).
    /// Distinct from <see cref="AssigneeUserId"/>, which may hold a group/pool name.
    /// Used by <c>AssignmentContextBuilder</c> Tier 2 to derive the team for the next stage.
    /// </summary>
    public string? CompletedBy { get; set; }
}

public enum ActivityExecutionStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Skipped,
    Cancelled
}