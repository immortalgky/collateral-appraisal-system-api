using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Engine.Core;
using Workflow.Workflow.Models;

namespace Workflow.Workflow.Services;

/// <summary>
/// High-performance structured logging interface for workflow operations with correlation context.
/// </summary>
public interface IWorkflowLogger
{
    /// <summary>
    /// Creates a correlation scope that adds workflow context to all log entries within the scope.
    /// </summary>
    /// <param name="workflowInstanceId">The workflow instance identifier.</param>
    /// <param name="correlationId">Optional correlation identifier.</param>
    /// <param name="operationName">Name of the operation being performed.</param>
    /// <returns>A disposable scope that maintains correlation context.</returns>
    IDisposable CreateCorrelationScope(Guid workflowInstanceId, string? correlationId = null, string? operationName = null);

    /// <summary>
    /// Creates an activity-scoped correlation context for activity-specific logging.
    /// </summary>
    /// <param name="workflowInstanceId">The workflow instance identifier.</param>
    /// <param name="activityId">The activity identifier.</param>
    /// <param name="activityType">The activity type.</param>
    /// <param name="correlationId">Optional correlation identifier.</param>
    /// <returns>A disposable scope that maintains activity correlation context.</returns>
    IDisposable CreateActivityCorrelationScope(Guid workflowInstanceId, string activityId, string activityType, string? correlationId = null);

    // Workflow Lifecycle Events
    void LogWorkflowStarting(Guid workflowDefinitionId, string instanceName, string startedBy, string? correlationId);
    void LogWorkflowStarted(WorkflowInstance workflowInstance);
    void LogWorkflowCompleted(WorkflowInstance workflowInstance, TimeSpan totalDuration);
    void LogWorkflowFailed(WorkflowInstance workflowInstance, string errorMessage, Exception? exception = null);
    void LogWorkflowSuspended(WorkflowInstance workflowInstance, string reason);
    void LogWorkflowResumed(WorkflowInstance workflowInstance, string activityId, string completedBy);
    void LogWorkflowCancelled(WorkflowInstance workflowInstance, string reason, string cancelledBy);

    // Activity Lifecycle Events  
    void LogActivityStarting(string activityId, string activityType, Guid workflowInstanceId, bool isResume = false);
    void LogActivityCompleted(string activityId, string activityType, Guid workflowInstanceId, TimeSpan duration, ActivityResultStatus status);
    void LogActivityFailed(string activityId, string activityType, Guid workflowInstanceId, string errorMessage, Exception? exception = null);
    void LogActivityPending(string activityId, string activityType, Guid workflowInstanceId, string reason);
    void LogActivityRetrying(string activityId, string activityType, Guid workflowInstanceId, int retryAttempt, TimeSpan delay, string reason);

    // Engine Operation Events
    void LogEngineOperation(string operation, Guid workflowInstanceId, object? additionalData = null);
    void LogEngineStep(string stepName, Guid workflowInstanceId, TimeSpan? duration = null, object? additionalData = null);
    void LogTransactionBoundary(string transactionType, Guid workflowInstanceId, string description);

    // Performance and Metrics
    void LogPerformanceMetric(string metricName, Guid workflowInstanceId, TimeSpan duration, object? additionalData = null);
    void LogConcurrencyEvent(string eventType, Guid workflowInstanceId, int retryAttempt, string description);

    // External Integration Events
    void LogExternalCallStarting(string url, string method, Guid workflowInstanceId, string activityId);
    void LogExternalCallCompleted(string url, string method, Guid workflowInstanceId, string activityId, int statusCode, TimeSpan duration);
    void LogExternalCallFailed(string url, string method, Guid workflowInstanceId, string activityId, string errorMessage, Exception? exception = null);

    // Bookmark and Persistence Events
    void LogBookmarkCreated(string bookmarkId, string bookmarkType, Guid workflowInstanceId, string activityId);
    void LogBookmarkConsumed(string bookmarkId, Guid workflowInstanceId, string activityId, string consumedBy);
    void LogPersistenceOperation(string operation, Guid workflowInstanceId, TimeSpan duration, bool success);

    // Assignment Events
    void LogAssignmentStarting(Guid workflowInstanceId, string activityId, string assignmentStrategy);
    void LogAssignmentCompleted(Guid workflowInstanceId, string activityId, string assignmentStrategy, string? assigneeId, string? assigneeGroup, TimeSpan duration);
    void LogAssignmentFailed(Guid workflowInstanceId, string activityId, string assignmentStrategy, string reason);

    // Error and Warning Events
    void LogWarning(string message, Guid workflowInstanceId, object? additionalData = null);
    void LogError(string message, Guid workflowInstanceId, Exception? exception = null, object? additionalData = null);
    void LogCriticalError(string message, Guid workflowInstanceId, Exception exception, object? additionalData = null);
}