using Microsoft.Extensions.Logging;
using Workflow.Telemetry;

namespace Workflow.Workflow.Services;

/// <summary>
/// High-performance logging extensions using LoggerMessage.Define for zero-allocation logging.
/// Provides structured logging with consistent format and telemetry integration.
/// </summary>
public static partial class WorkflowLoggingExtensions
{
    // Workflow Lifecycle Events
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Starting workflow execution - DefinitionId: {WorkflowDefinitionId}, InstanceName: {InstanceName}, StartedBy: {StartedBy}, CorrelationId: {CorrelationId}")]
    public static partial void LogWorkflowStarting(ILogger logger, Guid workflowDefinitionId, string instanceName, string startedBy, string? correlationId);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "Workflow started successfully - WorkflowId: {WorkflowInstanceId}, DefinitionId: {WorkflowDefinitionId}, InstanceName: {InstanceName}, StartedBy: {StartedBy}, CorrelationId: {CorrelationId}, StartedAt: {StartedAt}")]
    public static partial void LogWorkflowStarted(ILogger logger, Guid workflowInstanceId, Guid workflowDefinitionId, string instanceName, string? startedBy, string? correlationId, DateTime startedAt);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Workflow completed successfully - WorkflowId: {WorkflowInstanceId}, DefinitionId: {WorkflowDefinitionId}, InstanceName: {InstanceName}, Duration: {DurationMs}ms, CorrelationId: {CorrelationId}")]
    public static partial void LogWorkflowCompleted(ILogger logger, Guid workflowInstanceId, Guid workflowDefinitionId, string instanceName, double durationMs, string? correlationId);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Error,
        Message = "Workflow failed - WorkflowId: {WorkflowInstanceId}, DefinitionId: {WorkflowDefinitionId}, InstanceName: {InstanceName}, Error: {ErrorMessage}, CorrelationId: {CorrelationId}")]
    public static partial void LogWorkflowFailed(ILogger logger, Guid workflowInstanceId, Guid workflowDefinitionId, string instanceName, string errorMessage, string? correlationId, Exception? exception);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Information,
        Message = "Workflow suspended - WorkflowId: {WorkflowInstanceId}, DefinitionId: {WorkflowDefinitionId}, InstanceName: {InstanceName}, Reason: {Reason}, CorrelationId: {CorrelationId}")]
    public static partial void LogWorkflowSuspended(ILogger logger, Guid workflowInstanceId, Guid workflowDefinitionId, string instanceName, string reason, string? correlationId);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Information,
        Message = "Workflow resumed - WorkflowId: {WorkflowInstanceId}, DefinitionId: {WorkflowDefinitionId}, InstanceName: {InstanceName}, ActivityId: {ActivityId}, CompletedBy: {CompletedBy}, CorrelationId: {CorrelationId}")]
    public static partial void LogWorkflowResumed(ILogger logger, Guid workflowInstanceId, Guid workflowDefinitionId, string instanceName, string activityId, string completedBy, string? correlationId);

    [LoggerMessage(
        EventId = 1007,
        Level = LogLevel.Information,
        Message = "Workflow cancelled - WorkflowId: {WorkflowInstanceId}, DefinitionId: {WorkflowDefinitionId}, InstanceName: {InstanceName}, Reason: {Reason}, CancelledBy: {CancelledBy}, CorrelationId: {CorrelationId}")]
    public static partial void LogWorkflowCancelled(ILogger logger, Guid workflowInstanceId, Guid workflowDefinitionId, string instanceName, string reason, string cancelledBy, string? correlationId);

    // Activity Lifecycle Events
    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Starting activity execution - ActivityId: {ActivityId}, ActivityType: {ActivityType}, WorkflowId: {WorkflowInstanceId}, IsResume: {IsResume}")]
    public static partial void LogActivityStarting(ILogger logger, string activityId, string activityType, Guid workflowInstanceId, bool isResume);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "Activity completed - ActivityId: {ActivityId}, ActivityType: {ActivityType}, WorkflowId: {WorkflowInstanceId}, Duration: {DurationMs}ms, Status: {Status}")]
    public static partial void LogActivityCompleted(ILogger logger, string activityId, string activityType, Guid workflowInstanceId, double durationMs, string status);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Error,
        Message = "Activity failed - ActivityId: {ActivityId}, ActivityType: {ActivityType}, WorkflowId: {WorkflowInstanceId}, Error: {ErrorMessage}")]
    public static partial void LogActivityFailed(ILogger logger, string activityId, string activityType, Guid workflowInstanceId, string errorMessage, Exception? exception);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Information,
        Message = "Activity pending - ActivityId: {ActivityId}, ActivityType: {ActivityType}, WorkflowId: {WorkflowInstanceId}, Reason: {Reason}")]
    public static partial void LogActivityPending(ILogger logger, string activityId, string activityType, Guid workflowInstanceId, string reason);

    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Warning,
        Message = "Activity retry - ActivityId: {ActivityId}, ActivityType: {ActivityType}, WorkflowId: {WorkflowInstanceId}, Attempt: {RetryAttempt}, Delay: {DelayMs}ms, Reason: {Reason}")]
    public static partial void LogActivityRetrying(ILogger logger, string activityId, string activityType, Guid workflowInstanceId, int retryAttempt, double delayMs, string reason);

    // Engine Operation Events
    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "Engine operation - Operation: {Operation}, WorkflowId: {WorkflowInstanceId}, Data: {AdditionalData}")]
    public static partial void LogEngineOperation(ILogger logger, string operation, Guid workflowInstanceId, object? additionalData);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Debug,
        Message = "Engine step - Step: {StepName}, WorkflowId: {WorkflowInstanceId}, Duration: {DurationMs}ms, Data: {AdditionalData}")]
    public static partial void LogEngineStep(ILogger logger, string stepName, Guid workflowInstanceId, double? durationMs, object? additionalData);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Debug,
        Message = "Transaction boundary - Type: {TransactionType}, WorkflowId: {WorkflowInstanceId}, Description: {Description}")]
    public static partial void LogTransactionBoundary(ILogger logger, string transactionType, Guid workflowInstanceId, string description);

    // Performance and Metrics Events
    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Information,
        Message = "Performance metric - Metric: {MetricName}, WorkflowId: {WorkflowInstanceId}, Duration: {DurationMs}ms, Data: {AdditionalData}")]
    public static partial void LogPerformanceMetric(ILogger logger, string metricName, Guid workflowInstanceId, double durationMs, object? additionalData);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Warning,
        Message = "Concurrency event - EventType: {EventType}, WorkflowId: {WorkflowInstanceId}, RetryAttempt: {RetryAttempt}, Description: {Description}")]
    public static partial void LogConcurrencyEvent(ILogger logger, string eventType, Guid workflowInstanceId, int retryAttempt, string description);

    // External Integration Events
    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Information,
        Message = "External call starting - URL: {Url}, Method: {Method}, WorkflowId: {WorkflowInstanceId}, ActivityId: {ActivityId}")]
    public static partial void LogExternalCallStarting(ILogger logger, string url, string method, Guid workflowInstanceId, string activityId);

    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Information,
        Message = "External call completed - URL: {Url}, Method: {Method}, WorkflowId: {WorkflowInstanceId}, ActivityId: {ActivityId}, StatusCode: {StatusCode}, Duration: {DurationMs}ms")]
    public static partial void LogExternalCallCompleted(ILogger logger, string url, string method, Guid workflowInstanceId, string activityId, int statusCode, double durationMs);

    [LoggerMessage(
        EventId = 5003,
        Level = LogLevel.Error,
        Message = "External call failed - URL: {Url}, Method: {Method}, WorkflowId: {WorkflowInstanceId}, ActivityId: {ActivityId}, Error: {ErrorMessage}")]
    public static partial void LogExternalCallFailed(ILogger logger, string url, string method, Guid workflowInstanceId, string activityId, string errorMessage, Exception? exception);

    // Bookmark and Persistence Events
    [LoggerMessage(
        EventId = 6001,
        Level = LogLevel.Information,
        Message = "Bookmark created - BookmarkId: {BookmarkId}, BookmarkType: {BookmarkType}, WorkflowId: {WorkflowInstanceId}, ActivityId: {ActivityId}")]
    public static partial void LogBookmarkCreated(ILogger logger, string bookmarkId, string bookmarkType, Guid workflowInstanceId, string activityId);

    [LoggerMessage(
        EventId = 6002,
        Level = LogLevel.Information,
        Message = "Bookmark consumed - BookmarkId: {BookmarkId}, WorkflowId: {WorkflowInstanceId}, ActivityId: {ActivityId}, ConsumedBy: {ConsumedBy}")]
    public static partial void LogBookmarkConsumed(ILogger logger, string bookmarkId, Guid workflowInstanceId, string activityId, string consumedBy);

    [LoggerMessage(
        EventId = 6003,
        Level = LogLevel.Debug,
        Message = "Persistence operation - Operation: {Operation}, WorkflowId: {WorkflowInstanceId}, Duration: {DurationMs}ms, Success: {Success}")]
    public static partial void LogPersistenceOperation(ILogger logger, string operation, Guid workflowInstanceId, double durationMs, bool success);

    // Assignment Events
    [LoggerMessage(
        EventId = 7001,
        Level = LogLevel.Information,
        Message = "Assignment starting - WorkflowId: {WorkflowInstanceId}, ActivityId: {ActivityId}, Strategy: {AssignmentStrategy}")]
    public static partial void LogAssignmentStarting(ILogger logger, Guid workflowInstanceId, string activityId, string assignmentStrategy);

    [LoggerMessage(
        EventId = 7002,
        Level = LogLevel.Information,
        Message = "Assignment completed - WorkflowId: {WorkflowInstanceId}, ActivityId: {ActivityId}, Strategy: {AssignmentStrategy}, AssigneeId: {AssigneeId}, AssigneeGroup: {AssigneeGroup}, Duration: {DurationMs}ms")]
    public static partial void LogAssignmentCompleted(ILogger logger, Guid workflowInstanceId, string activityId, string assignmentStrategy, string? assigneeId, string? assigneeGroup, double durationMs);

    [LoggerMessage(
        EventId = 7003,
        Level = LogLevel.Error,
        Message = "Assignment failed - WorkflowId: {WorkflowInstanceId}, ActivityId: {ActivityId}, Strategy: {AssignmentStrategy}, Reason: {Reason}")]
    public static partial void LogAssignmentFailed(ILogger logger, Guid workflowInstanceId, string activityId, string assignmentStrategy, string reason);

    // General Workflow Events
    [LoggerMessage(
        EventId = 8001,
        Level = LogLevel.Warning,
        Message = "Workflow warning - WorkflowId: {WorkflowInstanceId}, Message: {Message}, Data: {AdditionalData}")]
    public static partial void LogWorkflowWarning(ILogger logger, string message, Guid workflowInstanceId, object? additionalData);

    [LoggerMessage(
        EventId = 8002,
        Level = LogLevel.Error,
        Message = "Workflow error - WorkflowId: {WorkflowInstanceId}, Message: {Message}, Data: {AdditionalData}")]
    public static partial void LogWorkflowError(ILogger logger, string message, Guid workflowInstanceId, object? additionalData, Exception? exception);

    [LoggerMessage(
        EventId = 8003,
        Level = LogLevel.Critical,
        Message = "Workflow critical error - WorkflowId: {WorkflowInstanceId}, Message: {Message}, Data: {AdditionalData}")]
    public static partial void LogWorkflowCriticalError(ILogger logger, string message, Guid workflowInstanceId, object? additionalData, Exception exception);

    // Helper methods for enriching logs with telemetry data
    
    /// <summary>
    /// Adds OpenTelemetry activity tags for workflow operations.
    /// </summary>
    /// <param name="workflowInstanceId">The workflow instance ID.</param>
    /// <param name="activityId">Optional activity ID.</param>
    /// <param name="correlationId">Optional correlation ID.</param>
    public static void EnrichWithTelemetryTags(Guid workflowInstanceId, string? activityId = null, string? correlationId = null)
    {
        var currentActivity = System.Diagnostics.Activity.Current;
        if (currentActivity == null) return;

        currentActivity.SetTag(WorkflowTelemetryConstants.SemanticAttributes.WorkflowInstanceId, workflowInstanceId.ToString());
        
        if (!string.IsNullOrEmpty(activityId))
            currentActivity.SetTag(WorkflowTelemetryConstants.SemanticAttributes.WorkflowActivityId, activityId);
            
        if (!string.IsNullOrEmpty(correlationId))
            currentActivity.SetTag(WorkflowTelemetryConstants.SemanticAttributes.WorkflowInstanceCorrelationId, correlationId);
    }

    /// <summary>
    /// Creates a structured data object for logging complex workflow state.
    /// </summary>
    /// <param name="properties">The properties to include.</param>
    /// <returns>A structured object for logging.</returns>
    public static object CreateStructuredData(params (string key, object? value)[] properties)
    {
        var data = new Dictionary<string, object?>();
        foreach (var (key, value) in properties)
        {
            data[key] = value;
        }
        return data;
    }
}