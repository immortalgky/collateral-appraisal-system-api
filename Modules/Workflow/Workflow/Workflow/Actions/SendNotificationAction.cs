using Workflow.Workflow.Actions.Core;
using Workflow.Workflow.Activities.Core;

namespace Workflow.Workflow.Actions;

/// <summary>
/// Action that sends notifications during workflow execution
/// Supports email, SMS, in-app notifications, and other communication channels
/// </summary>
public class SendNotificationAction : WorkflowActionBase
{
    private readonly INotificationService _notificationService;

    public SendNotificationAction(INotificationService notificationService, ILogger<SendNotificationAction> logger) : base(logger)
    {
        _notificationService = notificationService;
    }

    public override string ActionType => "SendNotification";
    public override string Name => "Send Notification";
    public override string Description => "Sends notifications via email, SMS, in-app messaging, or other channels";

    protected override async Task<ActionExecutionResult> ExecuteActionAsync(
        ActivityContext context,
        Dictionary<string, object> actionParameters,
        CancellationToken cancellationToken = default)
    {
        var notificationType = GetParameter<string>(actionParameters, "notificationType", "InApp");
        var recipients = GetParameter<List<string>>(actionParameters, "recipients", new List<string>());
        var recipientExpression = GetParameter<string>(actionParameters, "recipientExpression");
        var subject = GetParameter<string>(actionParameters, "subject", "");
        var message = GetParameter<string>(actionParameters, "message", "");
        var template = GetParameter<string>(actionParameters, "template");
        var templateData = GetParameter<Dictionary<string, object>>(actionParameters, "templateData", new Dictionary<string, object>());
        var priority = GetParameter<string>(actionParameters, "priority", "Normal");
        var category = GetParameter<string>(actionParameters, "category", "Workflow");

        Logger.LogDebug("Sending {NotificationType} notification for activity {ActivityId} to {RecipientCount} recipients",
            notificationType, context.ActivityId, recipients.Count);

        try
        {
            // Resolve recipient expressions if provided
            var finalRecipients = new List<string>(recipients);
            if (!string.IsNullOrEmpty(recipientExpression))
            {
                var resolvedRecipients = ResolveRecipientsFromExpression(recipientExpression, context);
                finalRecipients.AddRange(resolvedRecipients);
            }

            // Add current assignee if no recipients specified
            if (!finalRecipients.Any() && !string.IsNullOrEmpty(context.CurrentAssignee))
            {
                finalRecipients.Add(context.CurrentAssignee);
                Logger.LogDebug("No recipients specified, using current assignee: {Assignee}", context.CurrentAssignee);
            }

            if (!finalRecipients.Any())
            {
                return ActionExecutionResult.Failed("No recipients specified for notification");
            }

            // Resolve variable expressions in notification content
            var resolvedSubject = ResolveVariableExpressions(subject, context);
            var resolvedMessage = ResolveVariableExpressions(message, context);

            // Prepare template data with workflow context
            var finalTemplateData = new Dictionary<string, object>(templateData);
            AddWorkflowContextToTemplateData(finalTemplateData, context);

            // Create notification request
            var notificationRequest = new NotificationRequest
            {
                Type = notificationType,
                Recipients = finalRecipients,
                Subject = resolvedSubject,
                Message = resolvedMessage,
                Template = template,
                TemplateData = finalTemplateData,
                Priority = priority,
                Category = category,
                Metadata = new Dictionary<string, object>
                {
                    ["workflowInstanceId"] = context.WorkflowInstanceId,
                    ["activityId"] = context.ActivityId,
                    ["source"] = "WorkflowAction",
                    ["timestamp"] = DateTime.UtcNow
                }
            };

            // Send the notification
            var notificationResult = await _notificationService.SendNotificationAsync(notificationRequest, cancellationToken);

            if (!notificationResult.IsSuccess)
            {
                var errorMessage = $"Failed to send notification: {notificationResult.ErrorMessage}";
                Logger.LogError("Notification sending failed for activity {ActivityId}: {Error}",
                    context.ActivityId, notificationResult.ErrorMessage);
                
                return ActionExecutionResult.Failed(errorMessage);
            }

            var resultMessage = $"Sent {notificationType} notification to {finalRecipients.Count} recipient(s)";
            var outputData = new Dictionary<string, object>
            {
                ["notificationType"] = notificationType,
                ["recipientCount"] = finalRecipients.Count,
                ["recipients"] = finalRecipients,
                ["subject"] = resolvedSubject,
                ["notificationId"] = notificationResult.NotificationId ?? "",
                ["sentAt"] = DateTime.UtcNow,
                ["priority"] = priority,
                ["category"] = category
            };

            // Add delivery details if available
            if (notificationResult.DeliveryDetails.Any())
            {
                outputData["deliveryDetails"] = notificationResult.DeliveryDetails;
            }

            Logger.LogInformation("Successfully sent {NotificationType} notification for activity {ActivityId} to {RecipientCount} recipients",
                notificationType, context.ActivityId, finalRecipients.Count);

            return ActionExecutionResult.Success(resultMessage, outputData);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Error sending notification: {ex.Message}";
            Logger.LogError(ex, "Error sending notification for activity {ActivityId}",
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

        var notificationType = GetParameter<string>(actionParameters, "notificationType", "InApp");
        var recipients = GetParameter<List<string>>(actionParameters, "recipients", new List<string>());
        var recipientExpression = GetParameter<string>(actionParameters, "recipientExpression");
        var message = GetParameter<string>(actionParameters, "message", "");
        var template = GetParameter<string>(actionParameters, "template");
        var priority = GetParameter<string>(actionParameters, "priority", "Normal");

        // Validate notification type
        var validTypes = new[] { "Email", "SMS", "InApp", "Push", "Teams", "Slack" };
        if (!validTypes.Contains(notificationType, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"Invalid notification type '{notificationType}'. Valid types: {string.Join(", ", validTypes)}");
        }

        // Validate recipients or recipient expression
        if (!recipients.Any() && string.IsNullOrEmpty(recipientExpression))
        {
            warnings.Add("No recipients specified. Will default to current assignee if available.");
        }

        // Validate message content
        if (string.IsNullOrEmpty(message) && string.IsNullOrEmpty(template))
        {
            errors.Add("Either 'message' or 'template' must be provided");
        }

        // Validate priority
        var validPriorities = new[] { "Low", "Normal", "High", "Critical" };
        if (!validPriorities.Contains(priority, StringComparer.OrdinalIgnoreCase))
        {
            warnings.Add($"Invalid priority '{priority}'. Valid priorities: {string.Join(", ", validPriorities)}. Using 'Normal' as default.");
        }

        // Validate email addresses if notification type is Email
        if (notificationType.Equals("Email", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var recipient in recipients)
            {
                if (!IsValidEmail(recipient) && !recipient.Contains("${"))
                {
                    warnings.Add($"Recipient '{recipient}' does not appear to be a valid email address");
                }
            }
        }

        // Validate phone numbers if notification type is SMS
        if (notificationType.Equals("SMS", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var recipient in recipients)
            {
                if (!IsValidPhoneNumber(recipient) && !recipient.Contains("${"))
                {
                    warnings.Add($"Recipient '{recipient}' does not appear to be a valid phone number");
                }
            }
        }

        return Task.FromResult(errors.Any() ? 
            ActionValidationResult.Invalid(errors, warnings) : 
            ActionValidationResult.Valid(warnings));
    }

    private List<string> ResolveRecipientsFromExpression(string expression, ActivityContext context)
    {
        var recipients = new List<string>();
        
        try
        {
            // Handle common recipient expressions
            var resolvedExpression = ResolveVariableExpressions(expression, context);
            
            // Split by common delimiters
            var splitRecipients = resolvedExpression.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var recipient in splitRecipients)
            {
                var trimmedRecipient = recipient.Trim();
                if (!string.IsNullOrEmpty(trimmedRecipient))
                {
                    recipients.Add(trimmedRecipient);
                }
            }

            Logger.LogDebug("Resolved recipient expression '{Expression}' to {Count} recipients: {Recipients}",
                expression, recipients.Count, string.Join(", ", recipients));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to resolve recipient expression '{Expression}'", expression);
        }
        
        return recipients;
    }

    private static void AddWorkflowContextToTemplateData(Dictionary<string, object> templateData, ActivityContext context)
    {
        // Add workflow context variables to template data
        templateData.TryAdd("workflowInstanceId", context.WorkflowInstanceId);
        templateData.TryAdd("activityId", context.ActivityId);
        templateData.TryAdd("assignee", context.CurrentAssignee ?? "");
        templateData.TryAdd("timestamp", DateTime.UtcNow);
        
        // Add workflow variables (be careful with sensitive data)
        foreach (var variable in context.Variables)
        {
            if (!IsSensitiveVariable(variable.Key))
            {
                templateData.TryAdd($"workflow_{variable.Key}", variable.Value);
            }
        }
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

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidPhoneNumber(string phoneNumber)
    {
        // Basic phone number validation - can be enhanced
        var cleaned = new string(phoneNumber.Where(char.IsDigit).ToArray());
        return cleaned.Length >= 10 && cleaned.Length <= 15;
    }
}

/// <summary>
/// Interface for notification service - integrates with existing notification infrastructure
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends a notification
    /// </summary>
    /// <param name="request">Notification request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Notification result</returns>
    Task<NotificationResult> SendNotificationAsync(NotificationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Notification request model
/// </summary>
public class NotificationRequest
{
    public string Type { get; init; } = default!;
    public List<string> Recipients { get; init; } = new();
    public string Subject { get; init; } = default!;
    public string Message { get; init; } = default!;
    public string? Template { get; init; }
    public Dictionary<string, object> TemplateData { get; init; } = new();
    public string Priority { get; init; } = "Normal";
    public string Category { get; init; } = "Workflow";
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Notification result model
/// </summary>
public class NotificationResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public string? NotificationId { get; init; }
    public Dictionary<string, object> DeliveryDetails { get; init; } = new();
    public DateTime SentAt { get; init; }
    
    public static NotificationResult Success(string? notificationId = null, Dictionary<string, object>? deliveryDetails = null)
        => new()
        {
            IsSuccess = true,
            NotificationId = notificationId,
            DeliveryDetails = deliveryDetails ?? new Dictionary<string, object>(),
            SentAt = DateTime.UtcNow
        };
    
    public static NotificationResult Failed(string errorMessage)
        => new()
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            SentAt = DateTime.UtcNow
        };
}