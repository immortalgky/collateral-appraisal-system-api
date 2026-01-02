using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Workflow.Telemetry;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Engine.Core;
using Workflow.Workflow.Models;

namespace Workflow.Workflow.Services;

/// <summary>
/// High-performance structured logging implementation for workflow operations.
/// Uses correlation scopes and optimized logging patterns for minimal performance impact.
/// </summary>
public class WorkflowLogger : IWorkflowLogger
{
    private readonly ILogger<WorkflowLogger> _logger;
    private readonly Activity? _currentActivity;

    public WorkflowLogger(ILogger<WorkflowLogger> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _currentActivity = Activity.Current;
    }

    public IDisposable CreateCorrelationScope(Guid workflowInstanceId, string? correlationId = null, string? operationName = null)
    {
        return new WorkflowCorrelationScope(_logger, workflowInstanceId, correlationId, operationName);
    }

    public IDisposable CreateActivityCorrelationScope(Guid workflowInstanceId, string activityId, string activityType, string? correlationId = null)
    {
        return new ActivityCorrelationScope(_logger, workflowInstanceId, activityId, activityType, correlationId);
    }

    // Workflow Lifecycle Events
    public void LogWorkflowStarting(Guid workflowDefinitionId, string instanceName, string startedBy, string? correlationId)
    {
        WorkflowLoggingExtensions.LogWorkflowStarting(_logger, workflowDefinitionId, instanceName, startedBy, correlationId);
    }

    public void LogWorkflowStarted(WorkflowInstance workflowInstance)
    {
        WorkflowLoggingExtensions.LogWorkflowStarted(_logger, workflowInstance.Id, workflowInstance.WorkflowDefinitionId, 
            workflowInstance.Name, workflowInstance.StartedBy, workflowInstance.CorrelationId, 
            workflowInstance.CreatedOn ?? workflowInstance.StartedOn);
    }

    public void LogWorkflowCompleted(WorkflowInstance workflowInstance, TimeSpan totalDuration)
    {
        WorkflowLoggingExtensions.LogWorkflowCompleted(_logger, workflowInstance.Id, workflowInstance.WorkflowDefinitionId,
            workflowInstance.Name, totalDuration.TotalMilliseconds, workflowInstance.CorrelationId);
    }

    public void LogWorkflowFailed(WorkflowInstance workflowInstance, string errorMessage, Exception? exception = null)
    {
        WorkflowLoggingExtensions.LogWorkflowFailed(_logger, workflowInstance.Id, workflowInstance.WorkflowDefinitionId,
            workflowInstance.Name, errorMessage, workflowInstance.CorrelationId, exception);
    }

    public void LogWorkflowSuspended(WorkflowInstance workflowInstance, string reason)
    {
        WorkflowLoggingExtensions.LogWorkflowSuspended(_logger, workflowInstance.Id, workflowInstance.WorkflowDefinitionId,
            workflowInstance.Name, reason, workflowInstance.CorrelationId);
    }

    public void LogWorkflowResumed(WorkflowInstance workflowInstance, string activityId, string completedBy)
    {
        WorkflowLoggingExtensions.LogWorkflowResumed(_logger, workflowInstance.Id, workflowInstance.WorkflowDefinitionId,
            workflowInstance.Name, activityId, completedBy, workflowInstance.CorrelationId);
    }

    public void LogWorkflowCancelled(WorkflowInstance workflowInstance, string reason, string cancelledBy)
    {
        WorkflowLoggingExtensions.LogWorkflowCancelled(_logger, workflowInstance.Id, workflowInstance.WorkflowDefinitionId,
            workflowInstance.Name, reason, cancelledBy, workflowInstance.CorrelationId);
    }

    // Activity Lifecycle Events
    public void LogActivityStarting(string activityId, string activityType, Guid workflowInstanceId, bool isResume = false)
    {
        WorkflowLoggingExtensions.LogActivityStarting(_logger, activityId, activityType, workflowInstanceId, isResume);
    }

    public void LogActivityCompleted(string activityId, string activityType, Guid workflowInstanceId, TimeSpan duration, ActivityResultStatus status)
    {
        WorkflowLoggingExtensions.LogActivityCompleted(_logger, activityId, activityType, workflowInstanceId, 
            duration.TotalMilliseconds, status.ToString());
    }

    public void LogActivityFailed(string activityId, string activityType, Guid workflowInstanceId, string errorMessage, Exception? exception = null)
    {
        WorkflowLoggingExtensions.LogActivityFailed(_logger, activityId, activityType, workflowInstanceId, errorMessage, exception);
    }

    public void LogActivityPending(string activityId, string activityType, Guid workflowInstanceId, string reason)
    {
        WorkflowLoggingExtensions.LogActivityPending(_logger, activityId, activityType, workflowInstanceId, reason);
    }

    public void LogActivityRetrying(string activityId, string activityType, Guid workflowInstanceId, int retryAttempt, TimeSpan delay, string reason)
    {
        WorkflowLoggingExtensions.LogActivityRetrying(_logger, activityId, activityType, workflowInstanceId, retryAttempt, 
            delay.TotalMilliseconds, reason);
    }

    // Engine Operation Events
    public void LogEngineOperation(string operation, Guid workflowInstanceId, object? additionalData = null)
    {
        WorkflowLoggingExtensions.LogEngineOperation(_logger, operation, workflowInstanceId, additionalData);
    }

    public void LogEngineStep(string stepName, Guid workflowInstanceId, TimeSpan? duration = null, object? additionalData = null)
    {
        WorkflowLoggingExtensions.LogEngineStep(_logger, stepName, workflowInstanceId, duration?.TotalMilliseconds, additionalData);
    }

    public void LogTransactionBoundary(string transactionType, Guid workflowInstanceId, string description)
    {
        WorkflowLoggingExtensions.LogTransactionBoundary(_logger, transactionType, workflowInstanceId, description);
    }

    // Performance and Metrics
    public void LogPerformanceMetric(string metricName, Guid workflowInstanceId, TimeSpan duration, object? additionalData = null)
    {
        WorkflowLoggingExtensions.LogPerformanceMetric(_logger, metricName, workflowInstanceId, duration.TotalMilliseconds, additionalData);
    }

    public void LogConcurrencyEvent(string eventType, Guid workflowInstanceId, int retryAttempt, string description)
    {
        WorkflowLoggingExtensions.LogConcurrencyEvent(_logger, eventType, workflowInstanceId, retryAttempt, description);
    }

    // External Integration Events
    public void LogExternalCallStarting(string url, string method, Guid workflowInstanceId, string activityId)
    {
        WorkflowLoggingExtensions.LogExternalCallStarting(_logger, url, method, workflowInstanceId, activityId);
    }

    public void LogExternalCallCompleted(string url, string method, Guid workflowInstanceId, string activityId, int statusCode, TimeSpan duration)
    {
        WorkflowLoggingExtensions.LogExternalCallCompleted(_logger, url, method, workflowInstanceId, activityId, statusCode, duration.TotalMilliseconds);
    }

    public void LogExternalCallFailed(string url, string method, Guid workflowInstanceId, string activityId, string errorMessage, Exception? exception = null)
    {
        WorkflowLoggingExtensions.LogExternalCallFailed(_logger, url, method, workflowInstanceId, activityId, errorMessage, exception);
    }

    // Bookmark and Persistence Events
    public void LogBookmarkCreated(string bookmarkId, string bookmarkType, Guid workflowInstanceId, string activityId)
    {
        WorkflowLoggingExtensions.LogBookmarkCreated(_logger, bookmarkId, bookmarkType, workflowInstanceId, activityId);
    }

    public void LogBookmarkConsumed(string bookmarkId, Guid workflowInstanceId, string activityId, string consumedBy)
    {
        WorkflowLoggingExtensions.LogBookmarkConsumed(_logger, bookmarkId, workflowInstanceId, activityId, consumedBy);
    }

    public void LogPersistenceOperation(string operation, Guid workflowInstanceId, TimeSpan duration, bool success)
    {
        WorkflowLoggingExtensions.LogPersistenceOperation(_logger, operation, workflowInstanceId, duration.TotalMilliseconds, success);
    }

    // Assignment Events
    public void LogAssignmentStarting(Guid workflowInstanceId, string activityId, string assignmentStrategy)
    {
        WorkflowLoggingExtensions.LogAssignmentStarting(_logger, workflowInstanceId, activityId, assignmentStrategy);
    }

    public void LogAssignmentCompleted(Guid workflowInstanceId, string activityId, string assignmentStrategy, string? assigneeId, string? assigneeGroup, TimeSpan duration)
    {
        WorkflowLoggingExtensions.LogAssignmentCompleted(_logger, workflowInstanceId, activityId, assignmentStrategy, 
            assigneeId, assigneeGroup, duration.TotalMilliseconds);
    }

    public void LogAssignmentFailed(Guid workflowInstanceId, string activityId, string assignmentStrategy, string reason)
    {
        WorkflowLoggingExtensions.LogAssignmentFailed(_logger, workflowInstanceId, activityId, assignmentStrategy, reason);
    }

    // Error and Warning Events
    public void LogWarning(string message, Guid workflowInstanceId, object? additionalData = null)
    {
        WorkflowLoggingExtensions.LogWorkflowWarning(_logger, message, workflowInstanceId, additionalData);
    }

    public void LogError(string message, Guid workflowInstanceId, Exception? exception = null, object? additionalData = null)
    {
        WorkflowLoggingExtensions.LogWorkflowError(_logger, message, workflowInstanceId, additionalData, exception);
    }

    public void LogCriticalError(string message, Guid workflowInstanceId, Exception exception, object? additionalData = null)
    {
        WorkflowLoggingExtensions.LogWorkflowCriticalError(_logger, message, workflowInstanceId, additionalData, exception);
    }

    /// <summary>
    /// Correlation scope that maintains workflow context in log entries.
    /// </summary>
    private class WorkflowCorrelationScope : IDisposable
    {
        private readonly IDisposable _scope;

        public WorkflowCorrelationScope(ILogger logger, Guid workflowInstanceId, string? correlationId, string? operationName)
        {
            var scopeData = new Dictionary<string, object>
            {
                [WorkflowTelemetryConstants.LogProperties.WorkflowInstanceId] = workflowInstanceId
            };

            if (!string.IsNullOrEmpty(correlationId))
                scopeData[WorkflowTelemetryConstants.LogProperties.CorrelationId] = correlationId;

            if (!string.IsNullOrEmpty(operationName))
                scopeData[WorkflowTelemetryConstants.LogProperties.WorkflowOperation] = operationName;

            _scope = logger.BeginScope(scopeData);
        }

        public void Dispose() => _scope.Dispose();
    }

    /// <summary>
    /// Activity-specific correlation scope that maintains both workflow and activity context.
    /// </summary>
    private class ActivityCorrelationScope : IDisposable
    {
        private readonly IDisposable _scope;

        public ActivityCorrelationScope(ILogger logger, Guid workflowInstanceId, string activityId, string activityType, string? correlationId)
        {
            var scopeData = new Dictionary<string, object>
            {
                [WorkflowTelemetryConstants.LogProperties.WorkflowInstanceId] = workflowInstanceId,
                [WorkflowTelemetryConstants.LogProperties.WorkflowActivityId] = activityId,
                [WorkflowTelemetryConstants.LogProperties.WorkflowActivityType] = activityType
            };

            if (!string.IsNullOrEmpty(correlationId))
                scopeData[WorkflowTelemetryConstants.LogProperties.CorrelationId] = correlationId;

            _scope = logger.BeginScope(scopeData);
        }

        public void Dispose() => _scope.Dispose();
    }
}