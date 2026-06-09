using MassTransit;
using Notification.Data;
using Notification.Domain.Notifications.Models;
using Notification.Domain.Notifications.Services;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Notification.Domain.Notifications.EventHandlers;

/// <summary>
/// Notifies the RM that the admin has sent a shortlist of quotations for selection.
/// </summary>
public class ShortlistSentToRmNotificationHandler(
    INotificationService notificationService,
    ILogger<ShortlistSentToRmNotificationHandler> logger,
    InboxGuard<NotificationDbContext> inboxGuard) : IConsumer<ShortlistSentToRmIntegrationEvent>
{
    public async Task Consume(ConsumeContext<ShortlistSentToRmIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;

        logger.LogInformation(
            "Processing ShortlistSentToRm notification for QuotationRequestId={QuotationRequestId}, RmUsername={RmUsername}",
            message.QuotationRequestId, message.RmUsername);

        try
        {
            if (string.IsNullOrEmpty(message.RmUsername))
            {
                await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
                return;
            }

            await notificationService.SendNotificationToUserAsync(
                message.RmUsername,
                "Shortlist Ready for Review",
                "The admin has sent you a shortlist of quotations to review and select.",
                NotificationType.WorkflowTransition,
                metadata: new Dictionary<string, object>
                {
                    { "quotationRequestId", message.QuotationRequestId }
                });

            logger.LogInformation(
                "Sent ShortlistSentToRm notification to RM {RmUsername} for QuotationRequestId={QuotationRequestId}",
                message.RmUsername, message.QuotationRequestId);

            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error processing ShortlistSentToRm notification for QuotationRequestId={QuotationRequestId}",
                message.QuotationRequestId);
            throw;
        }
    }
}
