using MassTransit;
using Notification.Data;
using Notification.Domain.Notifications.Models;
using Notification.Domain.Notifications.Services;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Notification.Domain.Notifications.EventHandlers;

/// <summary>
/// Consumes document followup notification signals emitted by the Workflow module and
/// forwards them to the SignalR NotificationHub. The integration event's <c>Type</c>
/// string maps 1:1 to the <see cref="NotificationType"/> enum value the frontend filters on.
/// </summary>
public class DocumentFollowupNotificationIntegrationEventHandler
    : IConsumer<DocumentFollowupNotificationIntegrationEvent>
{
    private readonly INotificationService _notificationService;
    private readonly InboxGuard<NotificationDbContext> _inboxGuard;
    private readonly ILogger<DocumentFollowupNotificationIntegrationEventHandler> _logger;

    public DocumentFollowupNotificationIntegrationEventHandler(
        INotificationService notificationService,
        InboxGuard<NotificationDbContext> inboxGuard,
        ILogger<DocumentFollowupNotificationIntegrationEventHandler> logger)
    {
        _notificationService = notificationService;
        _inboxGuard = inboxGuard;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DocumentFollowupNotificationIntegrationEvent> context)
    {
        if (await _inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var msg = context.Message;

        if (!Enum.TryParse<NotificationType>(msg.Type, ignoreCase: false, out var type))
        {
            _logger.LogWarning(
                "Unknown DocumentFollowup notification type '{Type}', defaulting to SystemNotification",
                msg.Type);
            type = NotificationType.SystemNotification;
        }

        var metadata = new Dictionary<string, object>
        {
            ["followupId"] = msg.FollowupId,
            ["raisingTaskId"] = msg.RaisingTaskId,
            ["parentAppraisalId"] = msg.ParentAppraisalId
        };
        if (msg.FollowupWorkflowInstanceId.HasValue)
            metadata["followupWorkflowInstanceId"] = msg.FollowupWorkflowInstanceId.Value;
        if (!string.IsNullOrWhiteSpace(msg.Reason))
            metadata["reason"] = msg.Reason;

        await _notificationService.SendNotificationToUserAsync(
            msg.Recipient,
            msg.Title,
            msg.Message,
            type,
            actionUrl: null,
            metadata: metadata);

        _logger.LogInformation(
            "Forwarded {Type} notification to {Recipient} for followup {FollowupId}",
            msg.Type, msg.Recipient, msg.FollowupId);

        await _inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
    }
}
