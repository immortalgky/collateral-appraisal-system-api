using Assignment.Workflow.Actions.Core;
using Assignment.Workflow.Activities.Core;

namespace Assignment.Workflow.Actions;

/// <summary>
/// Action that publishes events during workflow execution
/// Enables integration with event-driven architectures and external systems
/// </summary>
public class PublishEventAction : WorkflowActionBase
{
    private readonly IEventPublisher _eventPublisher;

    public PublishEventAction(IEventPublisher eventPublisher, ILogger<PublishEventAction> logger) : base(logger)
    {
        _eventPublisher = eventPublisher;
    }

    public override string ActionType => "PublishEvent";
    public override string Name => "Publish Event";
    public override string Description => "Publishes events to message queues, event buses, or other event-driven systems";

    protected override async Task<ActionExecutionResult> ExecuteActionAsync(
        ActivityContext context,
        Dictionary<string, object> actionParameters,
        CancellationToken cancellationToken = default)
    {
        var eventType = GetParameter<string>(actionParameters, "eventType");
        var eventName = GetParameter<string>(actionParameters, "eventName", eventType);
        var eventData = GetParameter<Dictionary<string, object>>(actionParameters, "eventData", new Dictionary<string, object>());
        var includeWorkflowContext = GetParameter<bool>(actionParameters, "includeWorkflowContext", true);
        var includeWorkflowVariables = GetParameter<bool>(actionParameters, "includeWorkflowVariables", false);
        var destination = GetParameter<string>(actionParameters, "destination", "default");
        var correlationId = GetParameter<string>(actionParameters, "correlationId");
        var eventVersion = GetParameter<string>(actionParameters, "eventVersion", "1.0");
        var eventSource = GetParameter<string>(actionParameters, "eventSource", "WorkflowEngine");

        Logger.LogDebug("Publishing event '{EventType}' for activity {ActivityId} to destination '{Destination}'",
            eventType, context.ActivityId, destination);

        try
        {
            // Resolve variable expressions in event data
            var resolvedEventData = ResolveEventDataExpressions(eventData, context);

            // Add workflow context if requested
            if (includeWorkflowContext)
            {
                resolvedEventData["workflowContext"] = new Dictionary<string, object>
                {
                    ["instanceId"] = context.WorkflowInstanceId,
                    ["activityId"] = context.ActivityId,
                    ["assignee"] = context.CurrentAssignee ?? "",
                    ["timestamp"] = DateTime.UtcNow
                };
            }

            // Add workflow variables if requested (filter sensitive data)
            if (includeWorkflowVariables)
            {
                var filteredVariables = context.Variables
                    .Where(kvp => !IsSensitiveVariable(kvp.Key))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                
                resolvedEventData["workflowVariables"] = filteredVariables;
            }

            // Resolve correlation ID
            var resolvedCorrelationId = !string.IsNullOrEmpty(correlationId) 
                ? ResolveVariableExpressions(correlationId, context)
                : context.WorkflowInstanceId.ToString();

            // Create event request
            var eventRequest = new EventPublishRequest
            {
                EventType = eventType,
                EventName = eventName,
                EventData = resolvedEventData,
                Destination = destination,
                CorrelationId = resolvedCorrelationId,
                EventVersion = eventVersion,
                EventSource = eventSource,
                Metadata = new Dictionary<string, object>
                {
                    ["publishedBy"] = "WorkflowEngine",
                    ["workflowInstanceId"] = context.WorkflowInstanceId,
                    ["activityId"] = context.ActivityId,
                    ["publishedAt"] = DateTime.UtcNow
                }
            };

            // Publish the event
            var publishResult = await _eventPublisher.PublishEventAsync(eventRequest, cancellationToken);

            if (!publishResult.IsSuccess)
            {
                var errorMessage = $"Failed to publish event '{eventType}': {publishResult.ErrorMessage}";
                Logger.LogError("Event publishing failed for activity {ActivityId}: {Error}",
                    context.ActivityId, publishResult.ErrorMessage);
                
                return ActionExecutionResult.Failed(errorMessage);
            }

            var resultMessage = $"Published event '{eventType}' to '{destination}'";
            var outputData = new Dictionary<string, object>
            {
                ["eventType"] = eventType,
                ["eventName"] = eventName,
                ["destination"] = destination,
                ["correlationId"] = resolvedCorrelationId,
                ["eventId"] = publishResult.EventId ?? "",
                ["publishedAt"] = DateTime.UtcNow,
                ["eventVersion"] = eventVersion,
                ["eventSource"] = eventSource
            };

            // Add publish details if available
            if (publishResult.PublishDetails.Any())
            {
                outputData["publishDetails"] = publishResult.PublishDetails;
            }

            Logger.LogInformation("Successfully published event '{EventType}' for activity {ActivityId} to '{Destination}'",
                eventType, context.ActivityId, destination);

            return ActionExecutionResult.Success(resultMessage, outputData);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Error publishing event '{eventType}': {ex.Message}";
            Logger.LogError(ex, "Error publishing event for activity {ActivityId}",
                context.ActivityId);
            
            return ActionExecutionResult.Failed(errorMessage);
        }
    }

    public override Task<ActionValidationResult> ValidateAsync(
        Dictionary<string, object> actionParameters,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Validate required parameters
        ValidateRequiredParameter(actionParameters, "eventType", errors);

        var eventType = GetParameter<string>(actionParameters, "eventType");
        var eventName = GetParameter<string>(actionParameters, "eventName", eventType);
        var destination = GetParameter<string>(actionParameters, "destination", "default");
        var eventVersion = GetParameter<string>(actionParameters, "eventVersion", "1.0");

        // Validate event type format
        if (!string.IsNullOrEmpty(eventType))
        {
            // Check for valid event type naming convention
            if (!eventType.Contains(".") && !char.IsUpper(eventType[0]))
            {
                warnings.Add($"Event type '{eventType}' should follow a naming convention like 'Domain.EventName' or 'PascalCase'");
            }

            if (eventType.Length > 100)
            {
                warnings.Add("Event type is very long. Consider using shorter, more concise event type names.");
            }
        }

        // Validate event version format
        if (!string.IsNullOrEmpty(eventVersion))
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(eventVersion, @"^\d+\.\d+(\.\d+)?$"))
            {
                warnings.Add($"Event version '{eventVersion}' should follow semantic versioning format (e.g., '1.0', '1.2.3')");
            }
        }

        // Validate destination format
        if (string.IsNullOrEmpty(destination))
        {
            warnings.Add("No destination specified. Event will be published to default destination.");
        }

        // Check for potential sensitive data in event data
        var eventData = GetParameter<Dictionary<string, object>>(actionParameters, "eventData", new Dictionary<string, object>());
        foreach (var key in eventData.Keys)
        {
            if (IsSensitiveVariable(key))
            {
                warnings.Add($"Event data contains potentially sensitive field '{key}'. Ensure this is intentional.");
            }
        }

        return Task.FromResult(errors.Any() ? 
            ActionValidationResult.Invalid(errors, warnings) : 
            ActionValidationResult.Valid(warnings));
    }

    private Dictionary<string, object> ResolveEventDataExpressions(Dictionary<string, object> eventData, ActivityContext context)
    {
        var resolvedData = new Dictionary<string, object>();

        foreach (var kvp in eventData)
        {
            try
            {
                if (kvp.Value is string stringValue)
                {
                    resolvedData[kvp.Key] = ResolveVariableExpressions(stringValue, context);
                }
                else if (kvp.Value is Dictionary<string, object> nestedDict)
                {
                    resolvedData[kvp.Key] = ResolveEventDataExpressions(nestedDict, context);
                }
                else
                {
                    resolvedData[kvp.Key] = kvp.Value;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to resolve expression in event data key '{Key}'", kvp.Key);
                resolvedData[kvp.Key] = kvp.Value; // Use original value if resolution fails
            }
        }

        return resolvedData;
    }

    private static bool IsSensitiveVariable(string variableName)
    {
        var sensitivePatterns = new[]
        {
            "password", "secret", "key", "token", "credential",
            "ssn", "social", "credit", "bank", "account", "pin"
        };

        return sensitivePatterns.Any(pattern => 
            variableName.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Interface for event publisher - integrates with existing event infrastructure
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes an event
    /// </summary>
    /// <param name="request">Event publish request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Publish result</returns>
    Task<EventPublishResult> PublishEventAsync(EventPublishRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Event publish request model
/// </summary>
public class EventPublishRequest
{
    public string EventType { get; init; } = default!;
    public string EventName { get; init; } = default!;
    public Dictionary<string, object> EventData { get; init; } = new();
    public string Destination { get; init; } = "default";
    public string CorrelationId { get; init; } = default!;
    public string EventVersion { get; init; } = "1.0";
    public string EventSource { get; init; } = "WorkflowEngine";
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Event publish result model
/// </summary>
public class EventPublishResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public string? EventId { get; init; }
    public string? MessageId { get; init; }
    public Dictionary<string, object> PublishDetails { get; init; } = new();
    public DateTime PublishedAt { get; init; }
    
    public static EventPublishResult Success(
        string? eventId = null, 
        string? messageId = null, 
        Dictionary<string, object>? publishDetails = null)
        => new()
        {
            IsSuccess = true,
            EventId = eventId,
            MessageId = messageId,
            PublishDetails = publishDetails ?? new Dictionary<string, object>(),
            PublishedAt = DateTime.UtcNow
        };
    
    public static EventPublishResult Failed(string errorMessage)
        => new()
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            PublishedAt = DateTime.UtcNow
        };
}