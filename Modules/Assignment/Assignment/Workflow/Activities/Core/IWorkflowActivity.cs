namespace Assignment.Workflow.Activities.Core;

public interface IWorkflowActivity
{
    string ActivityType { get; }
    string Name { get; }
    string Description { get; }
    
    Task<ActivityResult> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken = default);
    Task<ValidationResult> ValidateAsync(ActivityContext context, CancellationToken cancellationToken = default);
}

public class ActivityContext
{
    public Guid WorkflowInstanceId { get; init; }
    public string ActivityId { get; init; } = default!;
    public Dictionary<string, object> Properties { get; init; } = new();
    public Dictionary<string, object> Variables { get; init; } = new();
    public Dictionary<string, object> InputData { get; init; } = new();
    public string? CurrentUser { get; init; }
    public string? CurrentAssignee { get; init; }
    public CancellationToken CancellationToken { get; init; }
}

public class ActivityResult
{
    public ActivityResultStatus Status { get; init; }
    public Dictionary<string, object> OutputData { get; init; } = new();
    public string? NextActivityId { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Comments { get; init; }
    public Dictionary<string, object> VariableUpdates { get; init; } = new();

    public static ActivityResult Success(Dictionary<string, object>? outputData = null, string? nextActivityId = null, string? comments = null)
    {
        return new ActivityResult
        {
            Status = ActivityResultStatus.Completed,
            OutputData = outputData ?? new Dictionary<string, object>(),
            NextActivityId = nextActivityId,
            Comments = comments
        };
    }

    public static ActivityResult Pending(string? nextActivityId = null, Dictionary<string, object>? variableUpdates = null, Dictionary<string, object>? outputData = null)
    {
        return new ActivityResult
        {
            Status = ActivityResultStatus.Pending,
            NextActivityId = nextActivityId,
            VariableUpdates = variableUpdates ?? new Dictionary<string, object>(),
            OutputData = outputData ?? new Dictionary<string, object>()
        };
    }

    public static ActivityResult Failed(string errorMessage)
    {
        return new ActivityResult
        {
            Status = ActivityResultStatus.Failed,
            ErrorMessage = errorMessage
        };
    }
}

public enum ActivityResultStatus
{
    Completed,
    Pending,
    Failed,
    Skipped
}

public class ValidationResult
{
    public bool IsValid { get; init; }
    public List<string> Errors { get; init; } = new();
    
    public static ValidationResult Success() => new() { IsValid = true };
    public static ValidationResult Failure(params string[] errors) => new() { IsValid = false, Errors = errors.ToList() };
}