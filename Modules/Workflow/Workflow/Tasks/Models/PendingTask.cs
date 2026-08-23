using Workflow.Workflow.Events;

namespace Workflow.Tasks.Models;

public class PendingTask : Aggregate<Guid>
{
    public Guid CorrelationId { get; private set; } = Guid.Empty;
    public string TaskName { get; private set; } = default!;
    public string? TaskDescription { get; private set; }
    public TaskStatus TaskStatus { get; private set; } = default!;
    public string AssignedTo { get; private set; } = default!;
    public string AssignedType { get; private set; } = default!;
    public DateTime AssignedAt { get; private set; }

    /// <summary>
    /// The moment the CURRENT holder received this task. Stamped to AssignedAt at creation and
    /// re-stamped by <see cref="Reassign"/> when the caller supplies a timestamp (the supervisor
    /// reassign path). Display/ordering only — the SLA clock anchors on <see cref="AssignedAt"/> and
    /// <see cref="SlaStartAt"/>, which reassignment must never move.
    /// </summary>
    public DateTime AssigneeAssignedAt { get; private set; }

    public string? WorkingBy { get; private set; }
    public DateTime? LockedAt { get; private set; }

    /// <summary>
    /// The moment the CURRENT holder first opened the task (first transition to InProgress). Stamped
    /// once in <see cref="StartWorking"/> and never overwritten, so it records the initial open time
    /// even if the task is later re-opened. Null while the task is still Assigned/unopened — and
    /// reset to null on a supervisor hand-off, since the incoming holder has not opened it yet.
    /// </summary>
    public DateTime? OpenedAt { get; private set; }
    public Guid WorkflowInstanceId { get; private set; }
    public string ActivityId { get; private set; } = default!;
    public DateTime? DueAt { get; private set; }

    /// <summary>
    /// The SLA clock-start anchor — the point the budget runs from (AssignedAt for Assignment-anchored
    /// policies, the appointment date for AppointmentDate-anchored ones, or a window's start-entry for
    /// window-governed tasks). The at-risk monitor measures the 75% threshold from here, not AssignedAt.
    /// Null when there is no deadline.
    /// </summary>
    public DateTime? SlaStartAt { get; private set; }

    public string? SlaStatus { get; private set; }
    public DateTime? SlaBreachedAt { get; private set; }

    /// <summary>
    /// The resolved SLA policy budget in hours (e.g. 24/48/72) that produced <see cref="DueAt"/> —
    /// the governing window's budget for window-governed tasks, else the per-activity budget. Persisted
    /// so the task list can display the SLA policy alongside the due date. Null when no policy applies.
    /// </summary>
    public int? SlaDurationHours { get; private set; }

    public string Movement { get; private set; } = "F";

    /// <summary>
    /// For fan-out tasks (e.g. ext-collect-submissions): the company that owns this task slot.
    /// Null for non-fan-out tasks.
    /// </summary>
    public Guid? AssigneeCompanyId { get; private set; }

    /// <summary>
    /// For committee approval tasks: the committee code resolved when the approval activity started
    /// (e.g. "SUB_COMMITTEE"). Null for non-approval tasks. Lets read views derive the approval tier
    /// while still pending, without parsing workflow Variables JSON.
    /// </summary>
    public string? CommitteeCode { get; private set; }

    /// <summary>
    /// In-progress decision draft fields, saved by the task owner so an unfinished review survives
    /// page reloads. Overwritten wholesale on every save; cleared implicitly once the task completes
    /// and moves to <see cref="CompletedTask"/>.
    /// </summary>
    public string? DecisionTaken { get; private set; }

    public string? Comment { get; private set; }
    public string? ReasonCode { get; private set; }

    /// <summary>The draft "Assign Next To" selection.</summary>
    public string? DraftAssignee { get; private set; }

    private PendingTask()
    {
        // For EF Core
    }

    private PendingTask(Guid correlationId, string taskName, string assignedTo, string assignedType,
        DateTime assignedAt, Guid workflowInstanceId, string activityId, string? taskDescription = null,
        string movement = "F", Guid? assigneeCompanyId = null, string? committeeCode = null)
    {
        Id = Guid.CreateVersion7();
        CorrelationId = correlationId;
        TaskName = taskName;
        TaskDescription = taskDescription;
        TaskStatus = TaskStatus.Assigned;
        AssignedTo = assignedTo;
        AssignedType = assignedType;
        AssignedAt = assignedAt;
        AssigneeAssignedAt = assignedAt;
        WorkflowInstanceId = workflowInstanceId;
        ActivityId = activityId;
        Movement = movement;
        AssigneeCompanyId = assigneeCompanyId;
        CommitteeCode = committeeCode;
    }

    public static PendingTask Create(Guid correlationId, string taskName, string assignedTo,
        string assignedType, DateTime assignedAt, Guid workflowInstanceId, string activityId,
        DateTime? dueAt = null, string? taskDescription = null, string movement = "F",
        Guid? assigneeCompanyId = null, string? committeeCode = null, DateTime? slaStartAt = null,
        int? slaDurationHours = null)
    {
        var task = new PendingTask(correlationId, taskName, assignedTo, assignedType, assignedAt,
            workflowInstanceId, activityId, taskDescription, movement, assigneeCompanyId, committeeCode);
        task.DueAt = dueAt;
        // Default the clock-start to AssignedAt when the caller doesn't supply an explicit anchor.
        task.SlaStartAt = dueAt.HasValue ? (slaStartAt ?? assignedAt) : null;
        task.SlaStatus = dueAt.HasValue ? "OnTime" : null;
        // The resolved policy budget — display-only; independent of whether a deadline exists yet
        // (an appointment-anchored task can carry its budget before DueAt is computed).
        task.SlaDurationHours = slaDurationHours;
        return task;
    }

    /// <param name="holderChangedAt">
    /// When supplied, re-stamps <see cref="AssigneeAssignedAt"/> so the history timeline can order
    /// the outgoing audit row before the incoming holder's row. Only the supervisor reassign path
    /// passes it — that is the one path that snapshots an audit row into CompletedTasks and would
    /// otherwise leave two rows sharing an identical sort key. Claim/implicit-assign/fan-out advance
    /// keep the original stamp so their single history row keeps showing the original start time.
    /// </param>
    public void Reassign(string newAssignedTo, string newAssignedType, string? raiseEventFor = null,
        DateTime? holderChangedAt = null)
    {
        var previousAssignedTo = AssignedTo;

        AssignedTo = newAssignedTo;
        AssignedType = newAssignedType;
        TaskStatus = TaskStatus.Assigned;
        WorkingBy = null;
        LockedAt = null;
        // AssignedAt, DueAt, SlaStatus, SlaBreachedAt intentionally preserved —
        // reassignment must not reset the SLA clock.
        if (holderChangedAt.HasValue)
        {
            AssigneeAssignedAt = holderChangedAt.Value;
            // Same rule as WorkingBy/LockedAt above: this is the outgoing holder's state, not the
            // incoming one's. Without this, OpenedAt's ??= would keep the first holder's open time
            // forever and every later holder would inherit a time they never opened the task at.
            OpenedAt = null;
        }

        if (raiseEventFor == "supervisor")
        {
            AddDomainEvent(new PendingTaskReassignedDomainEvent(
                Id,
                CorrelationId,
                previousAssignedTo,
                newAssignedTo,
                WorkflowInstanceId,
                ActivityId,
                DueAt));
        }
    }

    /// <param name="openedAt">
    /// Required, and must come from <c>IDateTimeProvider.ApplicationNow</c> — the same clock that
    /// stamps AssignedAt/AssigneeAssignedAt. <c>DateTime.Now</c> is the SERVER's local time, which on
    /// a UTC host is hours away from the configured application timezone; comparing the two below
    /// would then misjudge every stamp.
    /// </param>
    public void StartWorking(string username, DateTime openedAt, string? previousAssignedTo = null)
    {
        WorkingBy = username;
        TaskStatus = TaskStatus.InProgress;
        // Record the first-open timestamp; keep the original once set even if re-opened later.
        // A stamp older than AssigneeAssignedAt cannot belong to the current holder — nobody opens a
        // task before receiving it — so it is a leftover from a previous holder and gets replaced.
        // This self-heals rows handed off before Reassign started clearing OpenedAt; without it the
        // stamp-once rule would preserve the stale value forever.
        if (OpenedAt is null || OpenedAt < AssigneeAssignedAt)
            OpenedAt = openedAt;
        AddDomainEvent(new TaskStartedDomainEvent(CorrelationId, AssignedTo, AssignedAt, previousAssignedTo));
    }

    public void Lock(string username)
    {
        if (WorkingBy != null && !string.Equals(WorkingBy, username, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Task is already locked by {WorkingBy}");

        WorkingBy = username;
        LockedAt = DateTime.Now;
    }

    public void ReleaseLock()
    {
        WorkingBy = null;
        LockedAt = null;
    }

    public bool IsLockedBy(string username) =>
        string.Equals(WorkingBy, username, StringComparison.OrdinalIgnoreCase);

    public bool IsLockExpired(TimeSpan timeout) =>
        LockedAt.HasValue && LockedAt.Value.Add(timeout) < DateTime.Now;

    public void MarkAtRisk()
    {
        if (SlaStatus == "OnTime")
            SlaStatus = "AtRisk";
    }

    public void MarkBreached(DateTime breachedAt)
    {
        if (SlaStatus != "Breached")
        {
            SlaStatus = "Breached";
            SlaBreachedAt = breachedAt;
        }
    }

    /// <summary>
    /// Re-stamps DueAt (and its clock-start anchor) and resets SlaStatus to OnTime. Used by
    /// appointment-anchored SLA recalculation when the appointment is set or rescheduled after task
    /// assignment. <paramref name="slaStartAt"/> falls back to the existing AssignedAt when null.
    /// <paramref name="slaDurationHours"/> re-stamps the resolved budget so the recalc path stays in
    /// parity with creation (fills a pre-feature null, and refreshes if a policy's hours were edited);
    /// null leaves the existing value untouched.
    /// </summary>
    public void RecalculateDueAt(DateTime? dueAt, DateTime? slaStartAt = null, int? slaDurationHours = null)
    {
        DueAt = dueAt;
        SlaStartAt = dueAt.HasValue ? (slaStartAt ?? AssignedAt) : null;
        // If a new deadline is set, reset status to OnTime; null means "no deadline yet".
        SlaStatus = dueAt.HasValue ? "OnTime" : null;
        SlaBreachedAt = null;
        if (slaDurationHours.HasValue)
            SlaDurationHours = slaDurationHours;
    }

    /// <summary>
    /// Full-replace save of the in-progress decision draft — mirrors how the final decision overwrites
    /// all fields on completion. Called on every autosave/manual-save; does not validate the values,
    /// since the draft is not the decision of record until the task actually completes.
    /// </summary>
    public void SaveDecisionDraft(string? decisionTaken, string? comment, string? reasonCode, string? draftAssignee)
    {
        DecisionTaken = decisionTaken;
        Comment = comment;
        ReasonCode = reasonCode;
        DraftAssignee = draftAssignee;
    }
}
