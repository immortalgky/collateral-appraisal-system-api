using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Workflow.Telemetry;

/// <summary>
/// Extension methods for workflow tracing that provide convenient patterns for span management.
/// Enables automatic span lifecycle management using 'using' statements and provides
/// fluent APIs for common tracing scenarios.
/// </summary>
public static class WorkflowTracingExtensions
{
    /// <summary>
    /// Executes an asynchronous workflow operation within a traced span with automatic completion.
    /// </summary>
    /// <typeparam name="T">The return type of the operation</typeparam>
    /// <param name="tracing">The workflow tracing service</param>
    /// <param name="operationName">The name of the workflow operation</param>
    /// <param name="workflowInstanceId">The workflow instance ID</param>
    /// <param name="workflowDefinitionId">The workflow definition ID</param>
    /// <param name="operation">The operation to execute within the span</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <returns>The result of the operation</returns>
    public static async Task<T> TraceWorkflowOperationAsync<T>(
        this IWorkflowTracing tracing,
        string operationName,
        Guid workflowInstanceId,
        Guid workflowDefinitionId,
        Func<IWorkflowSpan, Task<T>> operation,
        string? correlationId = null)
    {
        using var span = tracing.CreateWorkflowSpan(operationName, workflowInstanceId, workflowDefinitionId, correlationId);
        
        try
        {
            var result = await operation(span);
            span.Complete();
            return result;
        }
        catch (Exception ex)
        {
            span.RecordException(ex);
            throw;
        }
    }

    /// <summary>
    /// Executes a synchronous workflow operation within a traced span with automatic completion.
    /// </summary>
    /// <typeparam name="T">The return type of the operation</typeparam>
    /// <param name="tracing">The workflow tracing service</param>
    /// <param name="operationName">The name of the workflow operation</param>
    /// <param name="workflowInstanceId">The workflow instance ID</param>
    /// <param name="workflowDefinitionId">The workflow definition ID</param>
    /// <param name="operation">The operation to execute within the span</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <returns>The result of the operation</returns>
    public static T TraceWorkflowOperation<T>(
        this IWorkflowTracing tracing,
        string operationName,
        Guid workflowInstanceId,
        Guid workflowDefinitionId,
        Func<IWorkflowSpan, T> operation,
        string? correlationId = null)
    {
        using var span = tracing.CreateWorkflowSpan(operationName, workflowInstanceId, workflowDefinitionId, correlationId);
        
        try
        {
            var result = operation(span);
            span.Complete();
            return result;
        }
        catch (Exception ex)
        {
            span.RecordException(ex);
            throw;
        }
    }

    /// <summary>
    /// Executes an asynchronous activity operation within a traced span with automatic completion.
    /// </summary>
    /// <typeparam name="T">The return type of the operation</typeparam>
    /// <param name="tracing">The workflow tracing service</param>
    /// <param name="activityName">The name of the activity</param>
    /// <param name="activityType">The type of the activity</param>
    /// <param name="workflowInstanceId">The workflow instance ID</param>
    /// <param name="activityExecutionId">The activity execution ID</param>
    /// <param name="operation">The operation to execute within the span</param>
    /// <returns>The result of the operation</returns>
    public static async Task<T> TraceActivityExecutionAsync<T>(
        this IWorkflowTracing tracing,
        string activityName,
        string activityType,
        Guid workflowInstanceId,
        Guid activityExecutionId,
        Func<IWorkflowSpan, Task<T>> operation)
    {
        using var span = tracing.CreateActivitySpan(activityName, activityType, workflowInstanceId, activityExecutionId);
        
        try
        {
            var result = await operation(span);
            span.Complete();
            return result;
        }
        catch (Exception ex)
        {
            span.RecordException(ex);
            throw;
        }
    }

    /// <summary>
    /// Executes an asynchronous external call within a traced span with automatic completion.
    /// </summary>
    /// <typeparam name="T">The return type of the operation</typeparam>
    /// <param name="tracing">The workflow tracing service</param>
    /// <param name="operationType">The type of external operation</param>
    /// <param name="targetUrl">The target URL</param>
    /// <param name="httpMethod">The HTTP method</param>
    /// <param name="workflowInstanceId">The workflow instance ID</param>
    /// <param name="operation">The operation to execute within the span</param>
    /// <returns>The result of the operation</returns>
    public static async Task<T> TraceExternalCallAsync<T>(
        this IWorkflowTracing tracing,
        string operationType,
        string targetUrl,
        string httpMethod,
        Guid workflowInstanceId,
        Func<IWorkflowSpan, Task<T>> operation)
    {
        using var span = tracing.CreateExternalCallSpan(operationType, targetUrl, httpMethod, workflowInstanceId);
        
        try
        {
            var result = await operation(span);
            span.Complete();
            return result;
        }
        catch (Exception ex)
        {
            span.RecordException(ex);
            throw;
        }
    }

    /// <summary>
    /// Executes an asynchronous database operation within a traced span with automatic completion.
    /// </summary>
    /// <typeparam name="T">The return type of the operation</typeparam>
    /// <param name="tracing">The workflow tracing service</param>
    /// <param name="operationType">The database operation type</param>
    /// <param name="entityType">The entity type being operated on</param>
    /// <param name="workflowInstanceId">The workflow instance ID</param>
    /// <param name="operation">The operation to execute within the span</param>
    /// <returns>The result of the operation</returns>
    public static async Task<T> TraceDatabaseOperationAsync<T>(
        this IWorkflowTracing tracing,
        string operationType,
        string entityType,
        Guid workflowInstanceId,
        Func<IWorkflowSpan, Task<T>> operation)
    {
        using var span = tracing.CreateDatabaseSpan(operationType, entityType, workflowInstanceId);
        
        try
        {
            var result = await operation(span);
            span.Complete();
            return result;
        }
        catch (Exception ex)
        {
            span.RecordException(ex);
            throw;
        }
    }

    /// <summary>
    /// Adds workflow context attributes to an existing span.
    /// </summary>
    /// <param name="span">The workflow span</param>
    /// <param name="workflowInstanceId">The workflow instance ID</param>
    /// <param name="workflowDefinitionId">The workflow definition ID</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <returns>The span for fluent chaining</returns>
    public static IWorkflowSpan WithWorkflowContext(
        this IWorkflowSpan span,
        Guid workflowInstanceId,
        Guid workflowDefinitionId,
        string? correlationId = null)
    {
        span.SetWorkflowInstanceId(workflowInstanceId)
            .SetWorkflowDefinitionId(workflowDefinitionId);
            
        if (!string.IsNullOrEmpty(correlationId))
        {
            span.SetCorrelationId(correlationId);
        }
        
        return span;
    }

    /// <summary>
    /// Adds activity context attributes to an existing span.
    /// </summary>
    /// <param name="span">The workflow span</param>
    /// <param name="activityName">The activity name</param>
    /// <param name="activityType">The activity type</param>
    /// <param name="activityExecutionId">The activity execution ID</param>
    /// <returns>The span for fluent chaining</returns>
    public static IWorkflowSpan WithActivityContext(
        this IWorkflowSpan span,
        string activityName,
        string activityType,
        Guid activityExecutionId)
    {
        span.SetAttribute(WorkflowTelemetryConstants.SemanticAttributes.WorkflowActivityName, activityName)
            .SetActivityType(activityType)
            .SetActivityExecutionId(activityExecutionId);
            
        return span;
    }

    /// <summary>
    /// Records a workflow milestone event on the span.
    /// </summary>
    /// <param name="span">The workflow span</param>
    /// <param name="milestone">The milestone name</param>
    /// <param name="description">Optional milestone description</param>
    /// <param name="attributes">Optional additional attributes</param>
    /// <returns>The span for fluent chaining</returns>
    public static IWorkflowSpan RecordMilestone(
        this IWorkflowSpan span,
        string milestone,
        string? description = null,
        IDictionary<string, object>? attributes = null)
    {
        var eventAttributes = new Dictionary<string, object>
        {
            ["milestone"] = milestone
        };
        
        if (!string.IsNullOrEmpty(description))
        {
            eventAttributes["description"] = description;
        }
        
        if (attributes != null)
        {
            foreach (var kvp in attributes)
            {
                eventAttributes[kvp.Key] = kvp.Value;
            }
        }
        
        return span.AddEvent("workflow.milestone", eventAttributes);
    }

    /// <summary>
    /// Records timing information for a workflow operation.
    /// </summary>
    /// <param name="span">The workflow span</param>
    /// <param name="operationName">The operation name</param>
    /// <param name="duration">The operation duration</param>
    /// <param name="unit">The duration unit (default: milliseconds)</param>
    /// <returns>The span for fluent chaining</returns>
    public static IWorkflowSpan RecordTiming(
        this IWorkflowSpan span,
        string operationName,
        TimeSpan duration,
        string unit = "ms")
    {
        var attributes = new Dictionary<string, object>
        {
            ["operation"] = operationName,
            ["duration"] = unit switch
            {
                "ms" => duration.TotalMilliseconds,
                "s" => duration.TotalSeconds,
                "us" => duration.TotalMicroseconds,
                _ => duration.TotalMilliseconds
            },
            ["unit"] = unit
        };
        
        return span.AddEvent("workflow.timing", attributes);
    }

    /// <summary>
    /// Sets HTTP response attributes for external call spans.
    /// </summary>
    /// <param name="span">The workflow span</param>
    /// <param name="statusCode">The HTTP status code</param>
    /// <param name="duration">The call duration</param>
    /// <returns>The span for fluent chaining</returns>
    public static IWorkflowSpan WithHttpResponse(
        this IWorkflowSpan span,
        int statusCode,
        TimeSpan duration)
    {
        span.SetAttribute(WorkflowTelemetryConstants.SemanticAttributes.ExternalCallStatusCode, statusCode)
            .SetAttribute(WorkflowTelemetryConstants.SemanticAttributes.ExternalCallDuration, duration.TotalMilliseconds);
            
        // Set span status based on HTTP status code
        var spanStatus = statusCode >= 200 && statusCode < 300 
            ? ActivityStatusCode.Ok 
            : ActivityStatusCode.Error;
            
        span.SetStatus(spanStatus, $"HTTP {statusCode}");
        
        return span;
    }

    /// <summary>
    /// Creates a scoped workflow tracer that automatically propagates context.
    /// </summary>
    /// <param name="tracing">The workflow tracing service</param>
    /// <param name="workflowInstanceId">The workflow instance ID to propagate</param>
    /// <param name="correlationId">The correlation ID to propagate</param>
    /// <returns>A disposable context that ensures proper baggage propagation</returns>
    public static IDisposable CreateWorkflowTracingScope(
        this IWorkflowTracing tracing,
        Guid workflowInstanceId,
        string? correlationId = null)
    {
        tracing.SetBaggage("workflow_instance_id", workflowInstanceId.ToString());
        
        if (!string.IsNullOrEmpty(correlationId))
        {
            tracing.SetBaggage("correlation_id", correlationId);
        }
        
        return new WorkflowTracingScope(tracing, workflowInstanceId, correlationId);
    }
}

/// <summary>
/// Disposable scope for workflow tracing context management.
/// </summary>
internal class WorkflowTracingScope : IDisposable
{
    private readonly IWorkflowTracing _tracing;
    private readonly Guid _workflowInstanceId;
    private readonly string? _correlationId;
    private bool _disposed;

    internal WorkflowTracingScope(IWorkflowTracing tracing, Guid workflowInstanceId, string? correlationId)
    {
        _tracing = tracing;
        _workflowInstanceId = workflowInstanceId;
        _correlationId = correlationId;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // Cleanup could be implemented here if needed
            // For now, baggage cleanup is handled automatically by Activity disposal
            _disposed = true;
        }
    }
}