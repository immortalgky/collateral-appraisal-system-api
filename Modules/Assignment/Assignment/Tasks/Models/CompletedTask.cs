namespace Assignment.Tasks.Models;

public class CompletedTask : Aggregate<Guid>
{
    public Guid CorrelationId { get; private set; } = Guid.Empty!;
    public long RequestId { get; private set; }
    public string TaskName { get; private set; } = default!;
    public TaskStatus TaskStatus { get; private set; } = default!;
    public string AssignedTo { get; private set; } = default!;
    public string AssignedType { get; private set; } = default!;
    public DateTime AssignedAt { get; private set; }
    public string ActionTaken { get; private set; } = default!;
    public DateTime CompletedAt { get; private set; }

    private CompletedTask()
    {
        // For EF Core
    }

    private CompletedTask(Guid id, Guid correlationId, long requestId, string taskName, string assignedTo,
        string assignedType, DateTime assignedAt, string actionTaken, DateTime completedAt)
    {
        Id = id;
        CorrelationId = correlationId;
        RequestId = requestId;
        TaskName = taskName;
        TaskStatus = TaskStatus.Completed;
        AssignedTo = assignedTo;
        AssignedType = assignedType;
        AssignedAt = assignedAt;
        ActionTaken = actionTaken;
        CompletedAt = completedAt;
    }

    public static CompletedTask Create(Guid id, Guid correlationId, long requestId, string taskName, string assignedTo,
        string assignedType, DateTime assignedAt, string actionTaken, DateTime completedAt)
    {
        return new CompletedTask(id, correlationId, requestId, taskName, assignedTo, assignedType, assignedAt,
            actionTaken, completedAt);
    }

    public static CompletedTask CreateFromPendingTask(PendingTask pendingTask, string actionTaken, DateTime completedAt)
    {
        return new CompletedTask(
            pendingTask.Id,
            pendingTask.CorrelationId,
            pendingTask.RequestId,
            pendingTask.TaskName,
            pendingTask.AssignedTo,
            pendingTask.AssignedType,
            pendingTask.AssignedAt,
            actionTaken,
            completedAt
        );
    }
}