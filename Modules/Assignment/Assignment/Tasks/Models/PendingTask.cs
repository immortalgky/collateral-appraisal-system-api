namespace Assignment.Tasks.Models;

public class PendingTask : Aggregate<Guid>
{
    public Guid CorrelationId { get; private set; } = Guid.Empty!;
    public long RequestId { get; private set; }
    public string TaskName { get; private set; } = default!;
    public TaskStatus TaskStatus { get; private set; } = default!;
    public string AssignedTo { get; private set; } = default!;
    public string AssignedType { get; private set; } = default!;
    public DateTime AssignedAt { get; private set; }

    private PendingTask()
    {
        // For EF Core
    }

    private PendingTask(Guid correlationId, long requestId, string taskName, string assignedTo, string assignedType,
        DateTime assignedAt)
    {
        Id = Guid.NewGuid();
        CorrelationId = correlationId;
        RequestId = requestId;
        TaskName = taskName;
        TaskStatus = TaskStatus.Assigned;
        AssignedTo = assignedTo;
        AssignedType = assignedType;
        AssignedAt = assignedAt;
    }

    public static PendingTask Create(Guid correlationId, long requestId, string taskName, string assignedTo,
        string assignedType, DateTime assignedAt)
    {
        return new PendingTask(correlationId, requestId, taskName, assignedTo, assignedType, assignedAt);
    }

    public void Reassign(string newAssignedTo, string newAssignedType, DateTime assignedAt)
    {
        AssignedTo = newAssignedTo;
        AssignedType = newAssignedType;
        TaskStatus = TaskStatus.Assigned;
        AssignedAt = assignedAt;
    }
}