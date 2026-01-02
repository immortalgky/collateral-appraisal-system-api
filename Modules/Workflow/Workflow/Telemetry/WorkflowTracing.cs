using System.Diagnostics;

namespace Workflow.Telemetry;

/// <summary>
/// Implementation of workflow distributed tracing using System.Diagnostics.Activity API.
/// Provides OpenTelemetry-compatible tracing for workflow operations across distributed systems.
/// </summary>
public class WorkflowTracing : IWorkflowTracing
{
    private readonly ActivitySource _activitySource;

    public WorkflowTracing()
    {
        _activitySource = WorkflowTelemetryConstants.ActivitySource;
    }

    public IWorkflowSpan CreateWorkflowSpan(
        string operationName,
        Guid workflowInstanceId,
        Guid workflowDefinitionId,
        string? correlationId = null)
    {
        var activity = _activitySource.StartActivity(operationName);
        var span = new WorkflowSpan(activity);

        span.SetWorkflowInstanceId(workflowInstanceId)
            .SetWorkflowDefinitionId(workflowDefinitionId);

        if (!string.IsNullOrEmpty(correlationId))
        {
            span.SetCorrelationId(correlationId);
            SetBaggage("correlation_id", correlationId);
        }

        // Set baggage for workflow context propagation
        SetBaggage("workflow_instance_id", workflowInstanceId.ToString());
        SetBaggage("workflow_definition_id", workflowDefinitionId.ToString());

        return span;
    }

    public IWorkflowSpan CreateActivitySpan(
        string activityName,
        string activityType,
        Guid workflowInstanceId,
        Guid activityExecutionId)
    {
        var activity = _activitySource.StartActivity(WorkflowTelemetryConstants.ActivityNames.ActivityExecution);
        var span = new WorkflowSpan(activity);

        span.SetAttribute(WorkflowTelemetryConstants.SemanticAttributes.WorkflowActivityName, activityName)
            .SetActivityType(activityType)
            .SetWorkflowInstanceId(workflowInstanceId)
            .SetActivityExecutionId(activityExecutionId);

        return span;
    }

    public IWorkflowSpan CreateExternalCallSpan(
        string operationType,
        string targetUrl,
        string httpMethod,
        Guid workflowInstanceId)
    {
        var activity = _activitySource.StartActivity(WorkflowTelemetryConstants.ActivityNames.ExternalCall);
        var span = new WorkflowSpan(activity);

        span.SetAttribute(WorkflowTelemetryConstants.SemanticAttributes.ExternalCallUrl, targetUrl)
            .SetAttribute(WorkflowTelemetryConstants.SemanticAttributes.ExternalCallMethod, httpMethod)
            .SetAttribute(WorkflowTelemetryConstants.SemanticAttributes.WorkflowEngineOperation, operationType)
            .SetWorkflowInstanceId(workflowInstanceId);

        return span;
    }

    public IWorkflowSpan CreateDatabaseSpan(
        string operationType,
        string entityType,
        Guid workflowInstanceId)
    {
        var activityName = operationType.ToLowerInvariant() switch
        {
            "save" or "create" or "insert" => WorkflowTelemetryConstants.ActivityNames.WorkflowPersist,
            "load" or "get" or "select" => WorkflowTelemetryConstants.ActivityNames.WorkflowLoad,
            _ => $"workflow.database.{operationType}"
        };

        var activity = _activitySource.StartActivity(activityName);
        var span = new WorkflowSpan(activity);

        span.SetAttribute(WorkflowTelemetryConstants.SemanticAttributes.WorkflowPersistenceOperation, operationType)
            .SetAttribute("db.entity_type", entityType)
            .SetWorkflowInstanceId(workflowInstanceId);

        return span;
    }

    public IWorkflowSpan CreateBookmarkSpan(
        string operationType,
        string bookmarkName,
        Guid workflowInstanceId)
    {
        var activityName = operationType.ToLowerInvariant() switch
        {
            "create" => WorkflowTelemetryConstants.ActivityNames.BookmarkCreate,
            "resume" => WorkflowTelemetryConstants.ActivityNames.BookmarkResume,
            _ => $"workflow.bookmark.{operationType}"
        };

        var activity = _activitySource.StartActivity(activityName);
        var span = new WorkflowSpan(activity);

        span.SetAttribute(WorkflowTelemetryConstants.SemanticAttributes.WorkflowBookmarkName, bookmarkName)
            .SetAttribute(WorkflowTelemetryConstants.SemanticAttributes.WorkflowPersistenceOperation, operationType)
            .SetWorkflowInstanceId(workflowInstanceId);

        return span;
    }

    public void SetBaggage(string key, string value)
    {
        // Set baggage on the current activity context for propagation
        var current = Activity.Current;
        if (current != null)
        {
            current.SetBaggage(key, value);
        }
    }

    public string? GetBaggage(string key)
    {
        var current = Activity.Current;
        return current?.GetBaggageItem(key);
    }

    public string? GetCurrentTraceId()
    {
        var current = Activity.Current;
        return current?.TraceId.ToString();
    }

    public string? GetCurrentSpanId()
    {
        var current = Activity.Current;
        return current?.SpanId.ToString();
    }
}

/// <summary>
/// Implementation of IWorkflowSpan that wraps a System.Diagnostics.Activity.
/// Provides fluent API for setting workflow-specific semantic attributes.
/// </summary>
internal class WorkflowSpan : IWorkflowSpan
{
    private bool _disposed;
    
    public Activity? Activity { get; }

    internal WorkflowSpan(Activity? activity)
    {
        Activity = activity;
    }

    public IWorkflowSpan SetAttribute(string key, object value)
    {
        Activity?.SetTag(key, value?.ToString());
        return this;
    }

    public IWorkflowSpan SetWorkflowInstanceId(Guid workflowInstanceId)
    {
        return SetAttribute(WorkflowTelemetryConstants.SemanticAttributes.WorkflowInstanceId, workflowInstanceId);
    }

    public IWorkflowSpan SetWorkflowDefinitionId(Guid workflowDefinitionId)
    {
        return SetAttribute(WorkflowTelemetryConstants.SemanticAttributes.WorkflowDefinitionId, workflowDefinitionId);
    }

    public IWorkflowSpan SetActivityExecutionId(Guid activityExecutionId)
    {
        return SetAttribute(WorkflowTelemetryConstants.SemanticAttributes.WorkflowActivityExecutionId, activityExecutionId);
    }

    public IWorkflowSpan SetWorkflowStatus(string status)
    {
        return SetAttribute(WorkflowTelemetryConstants.SemanticAttributes.WorkflowInstanceStatus, status);
    }

    public IWorkflowSpan SetActivityType(string activityType)
    {
        return SetAttribute(WorkflowTelemetryConstants.SemanticAttributes.WorkflowActivityType, activityType);
    }

    public IWorkflowSpan SetCorrelationId(string correlationId)
    {
        return SetAttribute(WorkflowTelemetryConstants.SemanticAttributes.WorkflowInstanceCorrelationId, correlationId);
    }

    public IWorkflowSpan RecordException(Exception exception)
    {
        if (Activity != null)
        {
            Activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            Activity.SetTag(WorkflowTelemetryConstants.SemanticAttributes.WorkflowErrorType, exception.GetType().Name);
            Activity.SetTag(WorkflowTelemetryConstants.SemanticAttributes.WorkflowErrorMessage, exception.Message);
            
            // Add exception event with structured data
            var eventTags = new ActivityTagsCollection
            {
                [WorkflowTelemetryConstants.SemanticAttributes.WorkflowErrorType] = exception.GetType().Name,
                [WorkflowTelemetryConstants.SemanticAttributes.WorkflowErrorMessage] = exception.Message
            };
            
            if (!string.IsNullOrEmpty(exception.StackTrace))
            {
                eventTags[WorkflowTelemetryConstants.SemanticAttributes.WorkflowErrorStackTrace] = exception.StackTrace;
            }
            
            Activity.AddEvent(new ActivityEvent("exception", DateTimeOffset.UtcNow, eventTags));
        }
        
        return this;
    }

    public IWorkflowSpan SetStatus(ActivityStatusCode status, string? description = null)
    {
        Activity?.SetStatus(status, description);
        return this;
    }

    public IWorkflowSpan AddEvent(string name, IDictionary<string, object>? attributes = null)
    {
        if (Activity != null)
        {
            var tags = attributes != null 
                ? new ActivityTagsCollection(attributes.Select(kv => new KeyValuePair<string, object?>(kv.Key, kv.Value)))
                : new ActivityTagsCollection();
                
            Activity.AddEvent(new ActivityEvent(name, DateTimeOffset.UtcNow, tags));
        }
        
        return this;
    }

    public void Complete()
    {
        Activity?.SetStatus(ActivityStatusCode.Ok);
        Dispose();
    }

    public void Fail(string error, Exception? exception = null)
    {
        if (Activity != null)
        {
            Activity.SetStatus(ActivityStatusCode.Error, error);
            Activity.SetTag(WorkflowTelemetryConstants.SemanticAttributes.WorkflowErrorMessage, error);
        }
        
        if (exception != null)
        {
            RecordException(exception);
        }
        
        Dispose();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Activity?.Dispose();
            _disposed = true;
        }
    }
}