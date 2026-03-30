namespace Workflow.Tasks.Models;

public class PendingTask : Aggregate<Guid>
{
    public Guid CorrelationId { get; private set; } = Guid.Empty!;
    public string TaskName { get; private set; } = default!;
    public string? TaskDescription { get; private set; }
    public TaskStatus TaskStatus { get; private set; } = default!;
    public string AssignedTo { get; private set; } = default!;
    public string AssignedType { get; private set; } = default!;
    public DateTime AssignedAt { get; private set; }
    public string? WorkingBy { get; private set; }
    public Guid WorkflowInstanceId { get; private set; }
    public string ActivityId { get; private set; } = default!;
    public DateTime? DueAt { get; private set; }
    public string? SlaStatus { get; private set; }
    public DateTime? SlaBreachedAt { get; private set; }

    private PendingTask()
    {
        // For EF Core
    }

    private PendingTask(Guid correlationId, string taskName, string assignedTo, string assignedType,
        DateTime assignedAt, Guid workflowInstanceId, string activityId, string? taskDescription = null)
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
    }

    public static PendingTask Create(Guid correlationId, string taskName, string assignedTo,
        string assignedType, DateTime assignedAt, Guid workflowInstanceId, string activityId,
        DateTime? dueAt = null, string? taskDescription = null)
    {
        var task = new PendingTask(correlationId, taskName, assignedTo, assignedType, assignedAt,
            workflowInstanceId, activityId, taskDescription);
        task.DueAt = dueAt;
        task.SlaStatus = dueAt.HasValue ? "OnTime" : null;
        return task;
    }

    public void Reassign(string newAssignedTo, string newAssignedType, DateTime assignedAt, DateTime? dueAt = null)
    {
        AssignedTo = newAssignedTo;
        AssignedType = newAssignedType;
        TaskStatus = TaskStatus.Assigned;
        AssignedAt = assignedAt;
        WorkingBy = null;
        if (dueAt.HasValue)
        {
            DueAt = dueAt;
            SlaStatus = "OnTime";
            SlaBreachedAt = null;
        }
    }

    public void StartWorking(string username)
    {
        WorkingBy = username;
        TaskStatus = TaskStatus.InProgress;
    }

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
