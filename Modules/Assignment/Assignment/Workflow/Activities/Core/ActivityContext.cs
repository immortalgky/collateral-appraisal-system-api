namespace Assignment.Workflow.Activities.Core;

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