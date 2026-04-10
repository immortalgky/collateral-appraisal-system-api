namespace Workflow.Workflow.Pipeline;

/// <summary>
/// Context passed to each process step during activity completion.
/// </summary>
public record ProcessStepContext
{
    /// <summary>
    /// Workflow correlation ID (requestId). Always available.
    /// </summary>
    public Guid CorrelationId { get; init; }

    /// <summary>
    /// Appraisal ID from Variables["appraisalId"]. Null before appraisal creation.
    /// </summary>
    public Guid? AppraisalId { get; init; }

    public Guid WorkflowInstanceId { get; init; }
    public string ActivityName { get; init; } = default!;
    public string CompletedBy { get; init; } = default!;
    public Dictionary<string, object> Input { get; init; } = new();
    public Dictionary<string, object>? Variables { get; init; }
    public string? Parameters { get; init; }
}
