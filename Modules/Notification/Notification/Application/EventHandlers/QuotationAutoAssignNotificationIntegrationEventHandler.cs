using MassTransit;
using Notification.Data;
using Notification.Domain.Notifications.Models;
using Notification.Domain.Notifications.Services;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Notification.Domain.Notifications.EventHandlers;

/// <summary>
/// Forwards the quotation auto-assign signal (incomplete run) to the finalizer.
/// </summary>
public class QuotationAutoAssignNotificationIntegrationEventHandler(
    INotificationService notificationService,
    InboxGuard<NotificationDbContext> inboxGuard,
    ILogger<QuotationAutoAssignNotificationIntegrationEventHandler> logger)
    : IConsumer<QuotationAutoAssignNotificationIntegrationEvent>
{
    public Task Consume(ConsumeContext<QuotationAutoAssignNotificationIntegrationEvent> context) =>
        inboxGuard.RunOnceAsync(context.MessageId, GetType().Name, _ => HandleAsync(context), context.CancellationToken);

    private async Task HandleAsync(ConsumeContext<QuotationAutoAssignNotificationIntegrationEvent> context)
    {
        var msg = context.Message;

        await notificationService.SendNotificationToUserAsync(
            msg.Recipient, msg.Title, msg.Message, NotificationType.QuotationAutoAssignIncomplete, msg.ActionUrl,
            new Dictionary<string, object> { ["quotationRequestId"] = msg.QuotationRequestId });

        logger.LogInformation("Forwarded QuotationAutoAssignIncomplete notification to {Recipient} for quotation {QuotationRequestId}",
            msg.Recipient, msg.QuotationRequestId);
    }
}
