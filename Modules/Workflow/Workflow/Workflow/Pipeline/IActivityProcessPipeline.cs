namespace Workflow.Workflow.Pipeline;

/// <summary>
/// Runs configured process steps for an activity before workflow continuation.
/// </summary>
public interface IActivityProcessPipeline
{
    Task<ProcessStepResult> ExecuteAsync(
        Guid workflowInstanceId,
        string activityId,
        string completedBy,
        Dictionary<string, object> input,
        CancellationToken ct);
}
