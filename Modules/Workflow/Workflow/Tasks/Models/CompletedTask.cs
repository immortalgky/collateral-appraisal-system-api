namespace Workflow.Tasks.Models;

public class CompletedTask : Aggregate<Guid>
{
    public Guid CorrelationId { get; private set; } = Guid.Empty!;
    public string? ActivityId { get; private set; }
    public string TaskName { get; private set; } = default!;
    public string? TaskDescription { get; private set; }
    public TaskStatus TaskStatus { get; private set; } = default!;
    public string AssignedTo { get; private set; } = default!;
    public string AssignedType { get; private set; } = default!;
    public DateTime AssignedAt { get; private set; }

    /// <summary>
    /// The moment the assignee on THIS row received the task — carried over from
    /// <c>PendingTask.AssigneeAssignedAt</c>. Differs from <see cref="AssignedAt"/> only after a
    /// supervisor reassign, which preserves the SLA anchor but hands the task to a new holder.
    /// History timelines order and display on this column; SLA math still uses AssignedAt.
    /// </summary>
    public DateTime AssigneeAssignedAt { get; private set; }

    public string ActionTaken { get; private set; } = default!;
    public DateTime CompletedAt { get; private set; }

    /// <summary>
    /// When this row's holder first opened the task, carried over from <c>PendingTask.OpenedAt</c>.
    /// Null means they never opened it — either the row predates this column, or the task was handed
    /// on / completed without the holder ever touching it, which is itself worth showing.
    /// </summary>
    public DateTime? OpenedAt { get; private set; }

    public DateTime? DueAt { get; private set; }

    /// <summary>
    /// The SLA clock-start anchor in force for this leg, carried over from
    /// <c>PendingTask.SlaStartAt</c>. Differs from <see cref="AssignedAt"/> for appointment-anchored
    /// policies (anchors on the visit) and window-governed tasks (anchors on the window's start
    /// activity). Null on rows written before this column existed.
    /// </summary>
    public DateTime? SlaStartAt { get; private set; }

    /// <summary>The resolved SLA policy budget in hours that produced <see cref="DueAt"/>.</summary>
    public int? SlaDurationHours { get; private set; }

    public string? SlaStatus { get; private set; }
    public DateTime? SlaBreachedAt { get; private set; }
    public string? Remark { get; private set; }
    public string Movement { get; private set; } = "F";

    /// <summary>
    /// Optional reason code for the decision, interpreted via <see cref="Movement"/>
    /// (e.g. a CancelReason code when Movement is "C", a RoutebackReason code when "B").
    /// Null for forward movements or completions without a coded reason.
    /// </summary>
    public string? ReasonCode { get; private set; }

    /// <summary>
    /// Carried forward from PendingTask.AssigneeCompanyId so historical pool-task
    /// visibility queries can enforce per-company isolation on completed rows.
    /// Null for non-fan-out tasks.
    /// </summary>
    public Guid? AssigneeCompanyId { get; private set; }

    private CompletedTask()
    {
        // For EF Core
    }

    private CompletedTask(Guid id, Guid correlationId, string taskName, string assignedTo,
        string assignedType, DateTime assignedAt, string actionTaken, DateTime completedAt,
        DateTime? dueAt = null, string? slaStatus = null, DateTime? slaBreachedAt = null,
        string? taskDescription = null, string? remark = null, string movement = "F",
        string? activityId = null, Guid? assigneeCompanyId = null, string? reasonCode = null,
        DateTime? assigneeAssignedAt = null, DateTime? openedAt = null, DateTime? slaStartAt = null,
        int? slaDurationHours = null)
    {
        Id = id;
        CorrelationId = correlationId;
        ActivityId = activityId;
        TaskName = taskName;
        TaskDescription = taskDescription;
        TaskStatus = TaskStatus.Completed;
        AssignedTo = assignedTo;
        AssignedType = assignedType;
        AssignedAt = assignedAt;
        AssigneeAssignedAt = assigneeAssignedAt ?? assignedAt;
        ActionTaken = actionTaken;
        CompletedAt = completedAt;
        OpenedAt = openedAt;
        DueAt = dueAt;
        SlaStartAt = slaStartAt;
        SlaDurationHours = slaDurationHours;
        SlaStatus = slaStatus;
        SlaBreachedAt = slaBreachedAt;
        Remark = remark;
        Movement = movement;
        AssigneeCompanyId = assigneeCompanyId;
        ReasonCode = reasonCode;
    }

    public static CompletedTask Create(Guid id, Guid correlationId, string taskName, string assignedTo,
        string assignedType, DateTime assignedAt, string actionTaken, DateTime completedAt,
        string? remark = null, string movement = "F", string? reasonCode = null)
    {
        return new CompletedTask(id, correlationId, taskName, assignedTo, assignedType, assignedAt,
            actionTaken, completedAt, remark: remark, movement: movement, reasonCode: reasonCode);
    }

    public static CompletedTask CreateFromPendingTask(PendingTask pendingTask, string actionTaken,
        DateTime completedAt, string? remark = null, string? movement = null, string? reasonCode = null)
    {
        return new CompletedTask(
            pendingTask.Id,
            pendingTask.CorrelationId,
            pendingTask.TaskName,
            pendingTask.AssignedTo,
            pendingTask.AssignedType,
            pendingTask.AssignedAt,
            actionTaken,
            completedAt,
            pendingTask.DueAt,
            pendingTask.SlaStatus,
            pendingTask.SlaBreachedAt,
            pendingTask.TaskDescription,
            remark,
            movement ?? pendingTask.Movement,
            pendingTask.ActivityId,
            pendingTask.AssigneeCompanyId,
            reasonCode,
            pendingTask.AssigneeAssignedAt,
            pendingTask.OpenedAt,
            pendingTask.SlaStartAt,
            pendingTask.SlaDurationHours
        );
    }

    /// <summary>
    /// Creates an audit-only snapshot of a PendingTask, minting a fresh Id so the row
    /// can coexist with the still-live PendingTask row and a future completion row.
    /// Use this for mid-life audit events (e.g. Reassigned) where the PendingTask is
    /// NOT being removed from the table in the same transaction.
    /// </summary>
    public static CompletedTask CreateAuditFromPendingTask(PendingTask pendingTask, string actionTaken,
        DateTime completedAt, string? remark = null, string? movement = null)
    {
        return new CompletedTask(
            Guid.CreateVersion7(),
            pendingTask.CorrelationId,
            pendingTask.TaskName,
            pendingTask.AssignedTo,
            pendingTask.AssignedType,
            pendingTask.AssignedAt,
            actionTaken,
            completedAt,
            pendingTask.DueAt,
            pendingTask.SlaStatus,
            pendingTask.SlaBreachedAt,
            pendingTask.TaskDescription,
            remark,
            movement ?? pendingTask.Movement,
            pendingTask.ActivityId,
            pendingTask.AssigneeCompanyId,
            null,
            pendingTask.AssigneeAssignedAt,
            pendingTask.OpenedAt,
            pendingTask.SlaStartAt,
            pendingTask.SlaDurationHours
        );
    }
}
