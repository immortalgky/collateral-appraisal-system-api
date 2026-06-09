using Microsoft.Extensions.Logging;
using Notification.Contracts.Realtime;
using Workflow.Workflow.Pipeline;

namespace Workflow.Workflow.Services;

/// <summary>
/// IRealtimeNotifier-backed implementation of <see cref="IActivityProgressReporter"/>.
/// Pushes each pipeline lifecycle event to the completing user's SignalR group
/// (<c>user-{completedBy}</c>) so connected FE clients can show step-by-step progress.
///
/// All pushes are fire-and-forget from the pipeline's perspective: any exception is
/// caught, logged, and swallowed — a failed push NEVER affects the transaction or
/// the returned <c>PipelineResult</c>.
///
/// Client event name: <c>"ActivityStepProgress"</c>.
/// </summary>
public sealed class SignalRActivityProgressReporter(
    IRealtimeNotifier realtimeNotifier,
    ILogger<SignalRActivityProgressReporter> logger) : IActivityProgressReporter
{
    /// <summary>
    /// The SignalR client method name the FE subscribes to.
    /// </summary>
    public const string ClientEventName = "ActivityStepProgress";

    public async Task PipelineStarted(
        Guid workflowActivityExecutionId,
        string activityName,
        IReadOnlyList<StepInfo> steps,
        string completedBy,
        CancellationToken ct)
    {
        await TrySendAsync(completedBy, new
        {
            WorkflowActivityExecutionId = workflowActivityExecutionId,
            ActivityName = activityName,
            Phase = "PipelineStarted",
            Steps = steps.Select(MapStep).ToArray()
        }, ct);
    }

    public async Task StepStarted(
        Guid workflowActivityExecutionId,
        StepInfo step,
        string completedBy,
        CancellationToken ct)
    {
        await TrySendAsync(completedBy, new
        {
            WorkflowActivityExecutionId = workflowActivityExecutionId,
            Phase = "StepStarted",
            Step = MapStep(step)
        }, ct);
    }

    public async Task StepFinished(
        Guid workflowActivityExecutionId,
        StepInfo step,
        string outcome,
        int durationMs,
        string completedBy,
        CancellationToken ct)
    {
        await TrySendAsync(completedBy, new
        {
            WorkflowActivityExecutionId = workflowActivityExecutionId,
            Phase = "StepFinished",
            Step = MapStep(step),
            Outcome = outcome,
            DurationMs = durationMs
        }, ct);
    }

    public async Task PipelineFinished(
        Guid workflowActivityExecutionId,
        string result,
        string completedBy,
        CancellationToken ct)
    {
        await TrySendAsync(completedBy, new
        {
            WorkflowActivityExecutionId = workflowActivityExecutionId,
            Phase = "PipelineFinished",
            Result = result
        }, ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task TrySendAsync(string completedBy, object payload, CancellationToken ct)
    {
        try
        {
            await realtimeNotifier.SendToGroupAsync($"user-{completedBy}", ClientEventName, payload, ct);
        }
        catch (Exception ex)
        {
            // Intentionally swallowed — a failed push must never affect the pipeline.
            logger.LogWarning(ex,
                "ActivityStepProgress push failed for user {CompletedBy}; pipeline unaffected",
                completedBy);
        }
    }

    private static object MapStep(StepInfo s) => new
    {
        StepName = s.StepName,
        DisplayName = s.DisplayName,
        SortOrder = s.SortOrder,
        Kind = s.Kind
    };
}
