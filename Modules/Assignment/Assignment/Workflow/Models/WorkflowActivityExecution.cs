using Shared.DDD;

namespace Assignment.Workflow.Models;

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