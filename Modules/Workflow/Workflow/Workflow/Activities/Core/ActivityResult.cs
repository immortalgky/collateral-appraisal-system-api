namespace Workflow.Workflow.Activities.Core;

public class ActivityResult
{
    public ActivityResultStatus Status { get; init; }
    public Dictionary<string, object> OutputData { get; init; } = new();
    public string? NextActivityId { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Comments { get; init; }

    public static ActivityResult Success(Dictionary<string, object>? outputData = null, string? nextActivityId = null,
        string? comments = null)
    {
        return new ActivityResult
        {
            Status = ActivityResultStatus.Completed,
            OutputData = outputData ?? new Dictionary<string, object>(),
            NextActivityId = nextActivityId,
            Comments = comments
        };
    }

    public static ActivityResult Pending(Dictionary<string, object>? outputData = null, string? nextActivityId = null)
    {
        return new ActivityResult
        {
            Status = ActivityResultStatus.Pending,
            NextActivityId = nextActivityId,
            OutputData = outputData ?? new Dictionary<string, object>()
        };
    }

    public static ActivityResult Failed(string errorMessage, Dictionary<string, object>? outputData = null)
    {
        return new ActivityResult
        {
            Status = ActivityResultStatus.Failed,
            ErrorMessage = errorMessage,
            OutputData = outputData ?? new Dictionary<string, object>()
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