using System.Diagnostics;

namespace Workflow.Telemetry;

/// <summary>
/// Interface for workflow distributed tracing operations using OpenTelemetry standards.
/// Provides comprehensive span management for workflow execution tracking across distributed systems.
/// </summary>
public interface IWorkflowTracing
{
    /// <summary>
    /// Creates a root workflow execution span for tracking the entire workflow lifecycle.
    /// </summary>
    /// <param name="operationName">The operation name (e.g., "workflow.start", "workflow.execute")</param>
    /// <param name="workflowInstanceId">The unique workflow instance identifier</param>
    /// <param name="workflowDefinitionId">The workflow definition identifier</param>
    /// <param name="correlationId">The correlation ID for request tracing</param>
    /// <returns>A workflow span that should be disposed when the operation completes</returns>
    IWorkflowSpan CreateWorkflowSpan(
        string operationName,
        Guid workflowInstanceId,
        Guid workflowDefinitionId,
        string? correlationId = null);

    /// <summary>
    /// Creates an activity execution span for tracking individual activity execution.
    /// </summary>
    /// <param name="activityName">The name of the activity being executed</param>
    /// <param name="activityType">The type of the activity (e.g., "HumanTask", "Decision", "Action")</param>
    /// <param name="workflowInstanceId">The workflow instance identifier</param>
    /// <param name="activityExecutionId">The unique activity execution identifier</param>
    /// <returns>A workflow span for the activity execution</returns>
    IWorkflowSpan CreateActivitySpan(
        string activityName,
        string activityType,
        Guid workflowInstanceId,
        Guid activityExecutionId);

    /// <summary>
    /// Creates an external call span for tracking HTTP calls, webhook invocations, and other external operations.
    /// </summary>
    /// <param name="operationType">The type of external operation (e.g., "webhook", "http_call", "api_request")</param>
    /// <param name="targetUrl">The target URL or endpoint being called</param>
    /// <param name="httpMethod">The HTTP method being used</param>
    /// <param name="workflowInstanceId">The workflow instance identifier</param>
    /// <returns>A workflow span for the external call</returns>
    IWorkflowSpan CreateExternalCallSpan(
        string operationType,
        string targetUrl,
        string httpMethod,
        Guid workflowInstanceId);

    /// <summary>
    /// Creates a database operation span for tracking workflow persistence operations.
    /// </summary>
    /// <param name="operationType">The database operation type (e.g., "save", "load", "update", "delete")</param>
    /// <param name="entityType">The type of entity being operated on</param>
    /// <param name="workflowInstanceId">The workflow instance identifier</param>
    /// <returns>A workflow span for the database operation</returns>
    IWorkflowSpan CreateDatabaseSpan(
        string operationType,
        string entityType,
        Guid workflowInstanceId);

    /// <summary>
    /// Creates a bookmark operation span for tracking workflow suspension and resumption.
    /// </summary>
    /// <param name="operationType">The bookmark operation type (e.g., "create", "resume", "cancel")</param>
    /// <param name="bookmarkName">The name of the bookmark</param>
    /// <param name="workflowInstanceId">The workflow instance identifier</param>
    /// <returns>A workflow span for the bookmark operation</returns>
    IWorkflowSpan CreateBookmarkSpan(
        string operationType,
        string bookmarkName,
        Guid workflowInstanceId);

    /// <summary>
    /// Sets baggage data for workflow context propagation across service boundaries.
    /// Baggage is automatically propagated with all child spans.
    /// </summary>
    /// <param name="key">The baggage key</param>
    /// <param name="value">The baggage value</param>
    void SetBaggage(string key, string value);

    /// <summary>
    /// Gets baggage data for workflow context retrieval.
    /// </summary>
    /// <param name="key">The baggage key</param>
    /// <returns>The baggage value, or null if not found</returns>
    string? GetBaggage(string key);

    /// <summary>
    /// Gets the current trace ID for correlation with external systems.
    /// </summary>
    /// <returns>The current trace ID, or null if no active trace</returns>
    string? GetCurrentTraceId();

    /// <summary>
    /// Gets the current span ID for parent-child relationships.
    /// </summary>
    /// <returns>The current span ID, or null if no active span</returns>
    string? GetCurrentSpanId();
}

/// <summary>
/// Represents a workflow span that provides semantic attribute management and automatic completion.
/// Implements IDisposable for automatic span completion using 'using' statements.
/// </summary>
public interface IWorkflowSpan : IDisposable
{
    /// <summary>
    /// The underlying Activity for advanced operations.
    /// </summary>
    Activity? Activity { get; }

    /// <summary>
    /// Adds a workflow-specific semantic attribute to the span.
    /// </summary>
    /// <param name="key">The attribute key (preferably from SemanticAttributes constants)</param>
    /// <param name="value">The attribute value</param>
    /// <returns>The span for fluent chaining</returns>
    IWorkflowSpan SetAttribute(string key, object value);

    /// <summary>
    /// Sets the workflow instance ID attribute.
    /// </summary>
    /// <param name="workflowInstanceId">The workflow instance identifier</param>
    /// <returns>The span for fluent chaining</returns>
    IWorkflowSpan SetWorkflowInstanceId(Guid workflowInstanceId);

    /// <summary>
    /// Sets the workflow definition ID attribute.
    /// </summary>
    /// <param name="workflowDefinitionId">The workflow definition identifier</param>
    /// <returns>The span for fluent chaining</returns>
    IWorkflowSpan SetWorkflowDefinitionId(Guid workflowDefinitionId);

    /// <summary>
    /// Sets the activity execution ID attribute.
    /// </summary>
    /// <param name="activityExecutionId">The activity execution identifier</param>
    /// <returns>The span for fluent chaining</returns>
    IWorkflowSpan SetActivityExecutionId(Guid activityExecutionId);

    /// <summary>
    /// Sets the workflow status attribute.
    /// </summary>
    /// <param name="status">The workflow status</param>
    /// <returns>The span for fluent chaining</returns>
    IWorkflowSpan SetWorkflowStatus(string status);

    /// <summary>
    /// Sets the activity type attribute.
    /// </summary>
    /// <param name="activityType">The activity type</param>
    /// <returns>The span for fluent chaining</returns>
    IWorkflowSpan SetActivityType(string activityType);

    /// <summary>
    /// Sets the correlation ID attribute.
    /// </summary>
    /// <param name="correlationId">The correlation identifier</param>
    /// <returns>The span for fluent chaining</returns>
    IWorkflowSpan SetCorrelationId(string correlationId);

    /// <summary>
    /// Records an exception on the span and sets the status to error.
    /// </summary>
    /// <param name="exception">The exception that occurred</param>
    /// <returns>The span for fluent chaining</returns>
    IWorkflowSpan RecordException(Exception exception);

    /// <summary>
    /// Sets the span status to indicate success or failure.
    /// </summary>
    /// <param name="status">The span status</param>
    /// <param name="description">Optional status description</param>
    /// <returns>The span for fluent chaining</returns>
    IWorkflowSpan SetStatus(ActivityStatusCode status, string? description = null);

    /// <summary>
    /// Adds an event to the span with a timestamp and optional attributes.
    /// </summary>
    /// <param name="name">The event name</param>
    /// <param name="attributes">Optional event attributes</param>
    /// <returns>The span for fluent chaining</returns>
    IWorkflowSpan AddEvent(string name, IDictionary<string, object>? attributes = null);

    /// <summary>
    /// Marks the span as completed with a success status.
    /// </summary>
    void Complete();

    /// <summary>
    /// Marks the span as failed with error details.
    /// </summary>
    /// <param name="error">The error message</param>
    /// <param name="exception">Optional exception details</param>
    void Fail(string error, Exception? exception = null);
}