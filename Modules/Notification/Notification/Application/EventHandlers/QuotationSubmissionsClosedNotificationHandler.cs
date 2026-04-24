using MassTransit;
using Notification.Data;
using Notification.Domain.Notifications.Models;
using Notification.Domain.Notifications.Services;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Notification.Domain.Notifications.EventHandlers;

/// <summary>
/// Notifies the admin pool that quotation submissions have closed and bids are ready for review.
/// </summary>
public class QuotationSubmissionsClosedNotificationHandler(
    INotificationService notificationService,
    ILogger<QuotationSubmissionsClosedNotificationHandler> logger,
    InboxGuard<NotificationDbContext> inboxGuard) : IConsumer<QuotationSubmissionsClosedIntegrationEvent>
{
    private const string AdminGroupName = "Admin";

    public async Task Consume(ConsumeContext<QuotationSubmissionsClosedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;

        logger.LogInformation(
            "Processing QuotationSubmissionsClosed notification for QuotationRequestId={QuotationRequestId}",
            message.QuotationRequestId);

        try
        {
            var metadata = new Dictionary<string, object>
            {
                { "quotationRequestId", message.QuotationRequestId },
                { "requestId", message.RequestId }
            };

            if (message.AdminUserIds.Length > 0)
            {
                // Notify specific admins if provided
                foreach (var adminId in message.AdminUserIds)
                {
                    await notificationService.SendNotificationToUserAsync(
                        adminId,
                        "Quotation Submissions Closed",
                        "Quotation submissions have closed. Please review the bids and build a shortlist.",
                        NotificationType.WorkflowTransition,
                        metadata: metadata);
                }
            }
            else
            {
                // Broadcast to the admin group
                await notificationService.SendNotificationToGroupAsync(
                    AdminGroupName,
                    "Quotation Submissions Closed",
                    "Quotation submissions have closed. Please review the bids and build a shortlist.",
                    NotificationType.WorkflowTransition,
                    metadata: metadata);
            }

            logger.LogInformation(
                "Sent QuotationSubmissionsClosed notification for QuotationRequestId={QuotationRequestId}",
                message.QuotationRequestId);

            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error processing QuotationSubmissionsClosed notification for QuotationRequestId={QuotationRequestId}",
                message.QuotationRequestId);
            throw;
        }
    }
}
