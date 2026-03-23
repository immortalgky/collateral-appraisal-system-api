namespace Workflow.Workflow.Pipeline;

/// <summary>
/// Context passed to each process step during activity completion.
/// </summary>
public record ProcessStepContext
{
    public Guid AppraisalId { get; init; }
    public Guid WorkflowInstanceId { get; init; }
    public string ActivityName { get; init; } = default!;
    public string CompletedBy { get; init; } = default!;
    public Dictionary<string, object> Input { get; init; } = new();
    public string? Parameters { get; init; }
}
