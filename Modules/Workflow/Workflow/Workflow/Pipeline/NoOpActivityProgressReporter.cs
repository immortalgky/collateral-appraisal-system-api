namespace Workflow.Workflow.Pipeline;

/// <summary>
/// No-op implementation of <see cref="IActivityProgressReporter"/>.
/// Registered by default; used by unit tests and non-UI callers.
/// All methods return <see cref="Task.CompletedTask"/> immediately.
/// </summary>
public sealed class NoOpActivityProgressReporter : IActivityProgressReporter
{
    public Task PipelineStarted(
        Guid workflowActivityExecutionId,
        string activityName,
        IReadOnlyList<StepInfo> steps,
        string completedBy,
        CancellationToken ct) => Task.CompletedTask;

    public Task StepStarted(
        Guid workflowActivityExecutionId,
        StepInfo step,
        string completedBy,
        CancellationToken ct) => Task.CompletedTask;

    public Task StepFinished(
        Guid workflowActivityExecutionId,
        StepInfo step,
        string outcome,
        int durationMs,
        string completedBy,
        CancellationToken ct) => Task.CompletedTask;

    public Task PipelineFinished(
        Guid workflowActivityExecutionId,
        string result,
        string completedBy,
        CancellationToken ct) => Task.CompletedTask;
}
