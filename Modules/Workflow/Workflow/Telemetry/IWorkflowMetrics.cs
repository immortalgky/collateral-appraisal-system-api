using System.Diagnostics.Metrics;

namespace Workflow.Telemetry;

/// <summary>
/// Defines methods for recording workflow-related metrics using System.Diagnostics.Metrics API.
/// Follows OpenTelemetry semantic conventions for metric names and tags.
/// </summary>
public interface IWorkflowMetrics
{
    /// <summary>
    /// Records the start of a workflow execution.
    /// </summary>
    /// <param name="workflowType">The type of workflow being started.</param>
    /// <param name="workflowDefinitionId">The workflow definition identifier.</param>
    /// <param name="tags">Additional tags for the metric.</param>
    void RecordWorkflowStarted(string workflowType, string workflowDefinitionId, params KeyValuePair<string, object?>[] tags);

    /// <summary>
    /// Records the completion of a workflow execution.
    /// </summary>
    /// <param name="workflowType">The type of workflow that completed.</param>
    /// <param name="workflowDefinitionId">The workflow definition identifier.</param>
    /// <param name="status">The completion status.</param>
    /// <param name="tags">Additional tags for the metric.</param>
    void RecordWorkflowCompleted(string workflowType, string workflowDefinitionId, string status, params KeyValuePair<string, object?>[] tags);

    /// <summary>
    /// Records a workflow failure.
    /// </summary>
    /// <param name="workflowType">The type of workflow that failed.</param>
    /// <param name="workflowDefinitionId">The workflow definition identifier.</param>
    /// <param name="errorType">The type of error that occurred.</param>
    /// <param name="tags">Additional tags for the metric.</param>
    void RecordWorkflowFailed(string workflowType, string workflowDefinitionId, string errorType, params KeyValuePair<string, object?>[] tags);

    /// <summary>
    /// Records a workflow suspension.
    /// </summary>
    /// <param name="workflowType">The type of workflow that was suspended.</param>
    /// <param name="workflowDefinitionId">The workflow definition identifier.</param>
    /// <param name="reason">The reason for suspension.</param>
    /// <param name="tags">Additional tags for the metric.</param>
    void RecordWorkflowSuspended(string workflowType, string workflowDefinitionId, string reason, params KeyValuePair<string, object?>[] tags);

    /// <summary>
    /// Records a workflow resumption.
    /// </summary>
    /// <param name="workflowType">The type of workflow that was resumed.</param>
    /// <param name="workflowDefinitionId">The workflow definition identifier.</param>
    /// <param name="tags">Additional tags for the metric.</param>
    void RecordWorkflowResumed(string workflowType, string workflowDefinitionId, params KeyValuePair<string, object?>[] tags);

    /// <summary>
    /// Records a workflow cancellation.
    /// </summary>
    /// <param name="workflowType">The type of workflow that was cancelled.</param>
    /// <param name="workflowDefinitionId">The workflow definition identifier.</param>
    /// <param name="reason">The reason for cancellation.</param>
    /// <param name="tags">Additional tags for the metric.</param>
    void RecordWorkflowCancelled(string workflowType, string workflowDefinitionId, string reason, params KeyValuePair<string, object?>[] tags);

    /// <summary>
    /// Records the duration of a workflow execution.
    /// </summary>
    /// <param name="workflowType">The type of workflow.</param>
    /// <param name="workflowDefinitionId">The workflow definition identifier.</param>
    /// <param name="duration">The execution duration.</param>
    /// <param name="status">The completion status.</param>
    /// <param name="tags">Additional tags for the metric.</param>
    void RecordWorkflowDuration(string workflowType, string workflowDefinitionId, TimeSpan duration, string status, params KeyValuePair<string, object?>[] tags);

    /// <summary>
    /// Records the start of an activity execution.
    /// </summary>
    /// <param name="activityType">The type of activity being executed.</param>
    /// <param name="activityName">The name of the activity.</param>
    /// <param name="workflowType">The type of workflow containing the activity.</param>
    /// <param name="tags">Additional tags for the metric.</param>
    void RecordActivityStarted(string activityType, string activityName, string workflowType, params KeyValuePair<string, object?>[] tags);

    /// <summary>
    /// Records the completion of an activity execution.
    /// </summary>
    /// <param name="activityType">The type of activity that completed.</param>
    /// <param name="activityName">The name of the activity.</param>
    /// <param name="workflowType">The type of workflow containing the activity.</param>
    /// <param name="status">The completion status.</param>
    /// <param name="tags">Additional tags for the metric.</param>
    void RecordActivityCompleted(string activityType, string activityName, string workflowType, string status, params KeyValuePair<string, object?>[] tags);

    /// <summary>
    /// Records the duration of an activity execution.
    /// </summary>
    /// <param name="activityType">The type of activity.</param>
    /// <param name="activityName">The name of the activity.</param>
    /// <param name="workflowType">The type of workflow containing the activity.</param>
    /// <param name="duration">The execution duration.</param>
    /// <param name="status">The completion status.</param>
    /// <param name="tags">Additional tags for the metric.</param>
    void RecordActivityDuration(string activityType, string activityName, string workflowType, TimeSpan duration, string status, params KeyValuePair<string, object?>[] tags);

    /// <summary>
    /// Records bookmark processing operations.
    /// </summary>
    /// <param name="operation">The bookmark operation (create, resume, delete).</param>
    /// <param name="bookmarkName">The name of the bookmark.</param>
    /// <param name="workflowType">The type of workflow.</param>
    /// <param name="duration">The operation duration.</param>
    /// <param name="tags">Additional tags for the metric.</param>
    void RecordBookmarkOperation(string operation, string bookmarkName, string workflowType, TimeSpan duration, params KeyValuePair<string, object?>[] tags);

    /// <summary>
    /// Updates the current count of active workflows.
    /// </summary>
    /// <param name="count">The current active workflow count.</param>
    /// <param name="workflowType">The type of workflows (optional filter).</param>
    /// <param name="tags">Additional tags for the metric.</param>
    void UpdateActiveWorkflowsCount(int count, string? workflowType = null, params KeyValuePair<string, object?>[] tags);

    /// <summary>
    /// Updates the current count of pending activities.
    /// </summary>
    /// <param name="count">The current pending activities count.</param>
    /// <param name="activityType">The type of activities (optional filter).</param>
    /// <param name="tags">Additional tags for the metric.</param>
    void UpdatePendingActivitiesCount(int count, string? activityType = null, params KeyValuePair<string, object?>[] tags);

    /// <summary>
    /// Updates the current count of suspended workflows.
    /// </summary>
    /// <param name="count">The current suspended workflows count.</param>
    /// <param name="workflowType">The type of workflows (optional filter).</param>
    /// <param name="tags">Additional tags for the metric.</param>
    void UpdateSuspendedWorkflowsCount(int count, string? workflowType = null, params KeyValuePair<string, object?>[] tags);

    /// <summary>
    /// Records an external call operation.
    /// </summary>
    /// <param name="url">The URL of the external call.</param>
    /// <param name="method">The HTTP method used.</param>
    /// <param name="statusCode">The HTTP status code returned.</param>
    /// <param name="duration">The call duration.</param>
    /// <param name="tags">Additional tags for the metric.</param>
    void RecordExternalCall(string url, string method, int statusCode, TimeSpan duration, params KeyValuePair<string, object?>[] tags);

    /// <summary>
    /// Records a workflow retry attempt.
    /// </summary>
    /// <param name="workflowType">The type of workflow being retried.</param>
    /// <param name="retryReason">The reason for the retry.</param>
    /// <param name="retryCount">The current retry count.</param>
    /// <param name="tags">Additional tags for the metric.</param>
    void RecordWorkflowRetry(string workflowType, string retryReason, int retryCount, params KeyValuePair<string, object?>[] tags);
}