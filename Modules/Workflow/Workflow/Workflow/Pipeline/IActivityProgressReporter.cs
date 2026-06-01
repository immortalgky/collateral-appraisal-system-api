namespace Workflow.Workflow.Pipeline;

/// <summary>
/// Carries the immutable identity of one pipeline step as seen by progress subscribers.
/// DisplayName is the human-readable label shown to the user ("Checking: …").
/// </summary>
public sealed record StepInfo(
    string StepName,
    string DisplayName,
    int SortOrder,
    string Kind);

/// <summary>
/// Transport-agnostic hook called by the pipeline at each lifecycle boundary.
/// Implementations must be exception-safe — the pipeline swallows any exception thrown
/// by a reporter so it can never affect the transaction or the returned PipelineResult.
///
/// The default registration is <see cref="NoOpActivityProgressReporter"/> so that unit
/// tests and non-UI callers remain unaffected when no SignalR transport is present.
/// </summary>
public interface IActivityProgressReporter
{
    /// <summary>
    /// Called once before the first step runs, with the ordered list of all configured steps.
    /// </summary>
    Task PipelineStarted(
        Guid workflowActivityExecutionId,
        string activityName,
        IReadOnlyList<StepInfo> steps,
        string completedBy,
        CancellationToken ct);

    /// <summary>Called immediately before each individual step executes.</summary>
    Task StepStarted(
        Guid workflowActivityExecutionId,
        StepInfo step,
        string completedBy,
        CancellationToken ct);

    /// <summary>Called immediately after each individual step finishes (pass, fail, or error).</summary>
    Task StepFinished(
        Guid workflowActivityExecutionId,
        StepInfo step,
        string outcome,
        int durationMs,
        string completedBy,
        CancellationToken ct);

    /// <summary>Called once after the pipeline has completed (success, validations failed, or action failed).</summary>
    Task PipelineFinished(
        Guid workflowActivityExecutionId,
        string result,
        string completedBy,
        CancellationToken ct);
}
