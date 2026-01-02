using Workflow.Workflow.Models;

namespace Workflow.Workflow.Services;

public interface ITwoPhaseExternalCallService
{
    /// <summary>
    /// Phase 1: Record intent to make external call within transaction
    /// </summary>
    Task<WorkflowExternalCall> RecordExternalCallIntentAsync(
        Guid workflowInstanceId,
        string activityId,
        ExternalCallType type,
        string endpoint,
        string method,
        string? requestPayload = null,
        Dictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phase 2: Execute the external call (outside transaction)
    /// </summary>
    Task<ExternalCallResult> ExecuteExternalCallAsync(
        Guid externalCallId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Complete the external call and return result for workflow processing
    /// </summary>
    Task<ExternalCallResult> CompleteExternalCallAsync(
        Guid externalCallId,
        string responsePayload,
        TimeSpan duration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fail the external call with error details
    /// </summary>
    Task<ExternalCallResult> FailExternalCallAsync(
        Guid externalCallId,
        string errorMessage,
        TimeSpan duration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get pending external calls for processing
    /// </summary>
    Task<List<WorkflowExternalCall>> GetPendingExternalCallsAsync(
        int maxCount = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retry failed external calls
    /// </summary>
    Task<List<WorkflowExternalCall>> GetRetryableExternalCallsAsync(
        int maxRetries = 3,
        int maxCount = 100,
        CancellationToken cancellationToken = default);
}

public sealed record ExternalCallResult(
    bool Success,
    string? ResponsePayload = null,
    string? ErrorMessage = null,
    TimeSpan? Duration = null,
    bool CanRetry = false
);