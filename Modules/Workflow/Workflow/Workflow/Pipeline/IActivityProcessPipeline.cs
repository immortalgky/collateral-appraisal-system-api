namespace Workflow.Workflow.Pipeline;

/// <summary>
/// Orchestrates the Validations-then-Actions pipeline for an activity completion.
/// Executes inside the completion DB transaction so Action failures trigger rollback.
/// </summary>
public interface IActivityProcessPipeline
{
    Task<PipelineResult> ExecuteAsync(
        Guid workflowInstanceId,
        Guid workflowActivityExecutionId,
        string activityName,
        string completedBy,
        IReadOnlyList<string> userRoles,
        IReadOnlyDictionary<string, object?> input,
        CancellationToken ct);
}
