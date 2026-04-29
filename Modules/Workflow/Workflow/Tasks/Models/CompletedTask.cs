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
    public string ActionTaken { get; private set; } = default!;
    public DateTime CompletedAt { get; private set; }
    public DateTime? DueAt { get; private set; }
    public string? SlaStatus { get; private set; }
    public DateTime? SlaBreachedAt { get; private set; }
    public string? Remark { get; private set; }
    public string Movement { get; private set; } = "F";

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
        string? activityId = null, Guid? assigneeCompanyId = null)
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
        ActionTaken = actionTaken;
        CompletedAt = completedAt;
        DueAt = dueAt;
        SlaStatus = slaStatus;
        SlaBreachedAt = slaBreachedAt;
        Remark = remark;
        Movement = movement;
        AssigneeCompanyId = assigneeCompanyId;
    }

    public static CompletedTask Create(Guid id, Guid correlationId, string taskName, string assignedTo,
        string assignedType, DateTime assignedAt, string actionTaken, DateTime completedAt,
        string? remark = null, string movement = "F")
    {
        return new CompletedTask(id, correlationId, taskName, assignedTo, assignedType, assignedAt,
            actionTaken, completedAt, remark: remark, movement: movement);
    }

    public static CompletedTask CreateFromPendingTask(PendingTask pendingTask, string actionTaken,
        DateTime completedAt, string? remark = null, string? movement = null)
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
            pendingTask.AssigneeCompanyId
        );
    }
}
