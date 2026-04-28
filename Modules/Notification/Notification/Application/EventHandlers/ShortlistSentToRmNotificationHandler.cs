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
            "Processing ShortlistSentToRm notification for QuotationRequestId={QuotationRequestId}, RmUserId={RmUserId}",
            message.QuotationRequestId, message.RmUserId);

        try
        {
            await notificationService.SendNotificationToUserAsync(
                message.RmUserId.ToString(),
                "Quotation Shortlist Ready for Review",
                $"The admin has sent a shortlist of {message.ShortlistedCompanyQuotationIds.Length} quotation(s) for your selection. Please review and pick a tentative winner.",
                NotificationType.WorkflowTransition,
                metadata: new Dictionary<string, object>
                {
                    { "quotationRequestId", message.QuotationRequestId },
                    { "requestId", message.RequestId },
                    { "shortlistedCount", message.ShortlistedCompanyQuotationIds.Length }
                });

            logger.LogInformation(
                "Sent ShortlistSentToRm notification to RM {RmUserId} for QuotationRequestId={QuotationRequestId}",
                message.RmUserId, message.QuotationRequestId);

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
