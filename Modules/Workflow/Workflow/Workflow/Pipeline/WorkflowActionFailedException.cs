namespace Workflow.Workflow.Pipeline;

/// <summary>
/// Thrown when a pipeline Action step fails, causing TransactionalBehavior to roll back
/// the completion transaction. The exception carries the StepFailure so the endpoint
/// can map it to a Problem Details response.
/// </summary>
public sealed class WorkflowActionFailedException(StepFailure failure)
    : Exception($"Pipeline action step '{failure.StepName}' failed: [{failure.ErrorCode}] {failure.Message}")
{
    /// <summary>Details of the Action step that failed.</summary>
    public StepFailure Failure { get; } = failure;
}
