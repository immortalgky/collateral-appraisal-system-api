using Assignment.Workflow.Actions.Core;
using Assignment.Workflow.Activities.Core;

namespace Assignment.Workflow.Actions;

/// <summary>
/// Action that creates audit log entries during workflow execution
/// Essential for compliance, tracking, and debugging workflow behavior
/// </summary>
public class CreateAuditEntryAction : WorkflowActionBase
{
    private readonly IAuditService _auditService;

    public CreateAuditEntryAction(IAuditService auditService, ILogger<CreateAuditEntryAction> logger) : base(logger)
    {
        _auditService = auditService;
    }

    public override string ActionType => "CreateAuditEntry";
    public override string Name => "Create Audit Entry";
    public override string Description => "Creates audit log entries for workflow activities and state changes";

    protected override async Task<ActionExecutionResult> ExecuteActionAsync(
        ActivityContext context,
        Dictionary<string, object> actionParameters,
        CancellationToken cancellationToken = default)
    {
        var eventType = GetParameter<string>(actionParameters, "eventType", "WorkflowAction");
        var message = GetParameter<string>(actionParameters, "message", "");
        var description = GetParameter<string>(actionParameters, "description", "");
        var severity = GetParameter<string>(actionParameters, "severity", "Information");
        var category = GetParameter<string>(actionParameters, "category", "Workflow");
        var includeContext = GetParameter<bool>(actionParameters, "includeContext", true);
        var includeVariables = GetParameter<bool>(actionParameters, "includeVariables", false);

        Logger.LogDebug("Creating audit entry for activity {ActivityId}: {EventType}",
            context.ActivityId, eventType);

        try
        {
            // Resolve any variable expressions in the message and description
            var resolvedMessage = ResolveVariableExpressions(message, context);
            var resolvedDescription = ResolveVariableExpressions(description, context);

            var auditData = new Dictionary<string, object>
            {
                ["workflowInstanceId"] = context.WorkflowInstanceId,
                ["activityId"] = context.ActivityId,
                ["eventType"] = eventType,
                ["message"] = resolvedMessage,
                ["description"] = resolvedDescription,
                ["severity"] = severity,
                ["category"] = category,
                ["timestamp"] = DateTime.UtcNow,
                ["assignee"] = context.CurrentAssignee ?? ""
            };

            // Include workflow context if requested
            if (includeContext)
            {
                auditData["workflowContext"] = new Dictionary<string, object>
                {
                    ["instanceId"] = context.WorkflowInstanceId,
                    ["activityId"] = context.ActivityId,
                    ["assignee"] = context.CurrentAssignee ?? "",
                    ["activityProperties"] = context.Properties
                };
            }

            // Include workflow variables if requested (be careful with sensitive data)
            if (includeVariables)
            {
                // Filter out sensitive variables (you might want to enhance this filtering)
                var filteredVariables = context.Variables
                    .Where(kvp => !IsSensitiveVariable(kvp.Key))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                
                auditData["workflowVariables"] = filteredVariables;

                if (filteredVariables.Count != context.Variables.Count)
                {
                    Logger.LogDebug("Filtered {Count} sensitive variables from audit entry",
                        context.Variables.Count - filteredVariables.Count);
                }
            }

            // Add any custom properties from action parameters
            foreach (var param in actionParameters)
            {
                if (!IsReservedParameter(param.Key))
                {
                    auditData[$"custom_{param.Key}"] = param.Value;
                }
            }

            // Create the audit entry
            await _auditService.CreateAuditEntryAsync(
                entityType: "WorkflowActivity",
                entityId: context.ActivityId,
                action: eventType,
                details: resolvedMessage,
                additionalData: auditData,
                userId: context.CurrentAssignee,
                cancellationToken: cancellationToken);

            var resultMessage = $"Created audit entry: {eventType} - {resolvedMessage}";
            var outputData = new Dictionary<string, object>
            {
                ["auditEntryCreated"] = true,
                ["eventType"] = eventType,
                ["message"] = resolvedMessage,
                ["severity"] = severity,
                ["timestamp"] = DateTime.UtcNow
            };

            Logger.LogInformation("Successfully created audit entry for activity {ActivityId}: {EventType} - {Message}",
                context.ActivityId, eventType, resolvedMessage);

            return ActionExecutionResult.Success(resultMessage, outputData);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Failed to create audit entry: {ex.Message}";
            Logger.LogError(ex, "Error creating audit entry for activity {ActivityId}",
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

        var eventType = GetParameter<string>(actionParameters, "eventType", "WorkflowAction");
        var message = GetParameter<string>(actionParameters, "message", "");
        var severity = GetParameter<string>(actionParameters, "severity", "Information");

        // Validate severity level
        var validSeverities = new[] { "Verbose", "Debug", "Information", "Warning", "Error", "Critical" };
        if (!validSeverities.Contains(severity, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"Invalid severity '{severity}'. Valid values are: {string.Join(", ", validSeverities)}");
        }

        // Warn if message is empty
        if (string.IsNullOrEmpty(message))
        {
            warnings.Add("Audit message is empty. Consider providing a meaningful message for better audit trails.");
        }

        // Validate event type format
        if (string.IsNullOrEmpty(eventType))
        {
            warnings.Add("Event type is empty. Consider providing a specific event type for better categorization.");
        }
        else if (eventType.Length > 100)
        {
            warnings.Add("Event type is very long. Consider using shorter, more concise event type names.");
        }

        return Task.FromResult(errors.Any() ? 
            ActionValidationResult.Invalid(errors, warnings) : 
            ActionValidationResult.Valid(warnings));
    }

    private static bool IsSensitiveVariable(string variableName)
    {
        // Define patterns for sensitive variables that shouldn't be audited
        var sensitivePatterns = new[]
        {
            "password", "secret", "key", "token", "credential",
            "ssn", "social", "credit", "bank", "account"
        };

        return sensitivePatterns.Any(pattern => 
            variableName.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsReservedParameter(string parameterName)
    {
        var reservedParams = new[]
        {
            "eventType", "message", "description", "severity", "category",
            "includeContext", "includeVariables"
        };

        return reservedParams.Contains(parameterName, StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Interface for audit service - to be implemented by the application's audit system
/// This allows the action to integrate with existing audit infrastructure
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Creates an audit log entry
    /// </summary>
    /// <param name="entityType">Type of entity being audited</param>
    /// <param name="entityId">ID of the entity being audited</param>
    /// <param name="action">Action that was performed</param>
    /// <param name="details">Detailed description of the action</param>
    /// <param name="additionalData">Additional structured data</param>
    /// <param name="userId">User who performed the action</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CreateAuditEntryAsync(
        string entityType,
        string entityId,
        string action,
        string details,
        Dictionary<string, object> additionalData,
        string? userId = null,
        CancellationToken cancellationToken = default);
}