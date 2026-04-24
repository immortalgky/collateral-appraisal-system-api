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
    public string? WorkingBy { get; private set; }
    public DateTime? LockedAt { get; private set; }
    public Guid WorkflowInstanceId { get; private set; }
    public string ActivityId { get; private set; } = default!;
    public DateTime? DueAt { get; private set; }
    public string? SlaStatus { get; private set; }
    public DateTime? SlaBreachedAt { get; private set; }
    public string Movement { get; private set; } = "F";

    /// <summary>
    /// For fan-out tasks (e.g. ext-collect-submissions): the company that owns this task slot.
    /// Null for non-fan-out tasks.
    /// </summary>
    public Guid? AssigneeCompanyId { get; private set; }

    private PendingTask()
    {
        // For EF Core
    }

    private PendingTask(Guid correlationId, string taskName, string assignedTo, string assignedType,
        DateTime assignedAt, Guid workflowInstanceId, string activityId, string? taskDescription = null,
        string movement = "F", Guid? assigneeCompanyId = null)
    {
        Id = Guid.CreateVersion7();
        CorrelationId = correlationId;
        TaskName = taskName;
        TaskDescription = taskDescription;
        TaskStatus = TaskStatus.Assigned;
        AssignedTo = assignedTo;
        AssignedType = assignedType;
        AssignedAt = assignedAt;
        WorkflowInstanceId = workflowInstanceId;
        ActivityId = activityId;
        Movement = movement;
        AssigneeCompanyId = assigneeCompanyId;
    }

    public static PendingTask Create(Guid correlationId, string taskName, string assignedTo,
        string assignedType, DateTime assignedAt, Guid workflowInstanceId, string activityId,
        DateTime? dueAt = null, string? taskDescription = null, string movement = "F",
        Guid? assigneeCompanyId = null)
    {
        var task = new PendingTask(correlationId, taskName, assignedTo, assignedType, assignedAt,
            workflowInstanceId, activityId, taskDescription, movement, assigneeCompanyId);
        task.DueAt = dueAt;
        task.SlaStatus = dueAt.HasValue ? "OnTime" : null;
        return task;
    }

    public void Reassign(string newAssignedTo, string newAssignedType)
    {
        AssignedTo = newAssignedTo;
        AssignedType = newAssignedType;
        TaskStatus = TaskStatus.Assigned;
        WorkingBy = null;
        LockedAt = null;
        // AssignedAt, DueAt, SlaStatus, SlaBreachedAt intentionally preserved —
        // reassignment must not reset the SLA clock.
    }

    public void StartWorking(string username, string? previousAssignedTo = null)
    {
        WorkingBy = username;
        TaskStatus = TaskStatus.InProgress;
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
}
