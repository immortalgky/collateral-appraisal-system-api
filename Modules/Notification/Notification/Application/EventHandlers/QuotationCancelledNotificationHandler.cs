using MassTransit;
using Notification.Data;
using Notification.Domain.Notifications.Models;
using Notification.Domain.Notifications.Services;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Notification.Domain.Notifications.EventHandlers;

/// <summary>
/// Notifies all invited companies and the RM that a quotation has been cancelled.
/// The admin's task remains open for the admin to decide the next action.
/// </summary>
public class QuotationCancelledNotificationHandler(
    INotificationService notificationService,
    ILogger<QuotationCancelledNotificationHandler> logger,
    InboxGuard<NotificationDbContext> inboxGuard) : IConsumer<QuotationCancelledIntegrationEvent>
{
    public async Task Consume(ConsumeContext<QuotationCancelledIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;

        logger.LogInformation(
            "Processing QuotationCancelled notification for QuotationRequestId={QuotationRequestId}",
            message.QuotationRequestId);

        try
        {
            var reason = string.IsNullOrWhiteSpace(message.Reason)
                ? "No reason provided."
                : message.Reason;

            var metadata = new Dictionary<string, object>
            {
                { "quotationRequestId", message.QuotationRequestId },
                { "reason", reason }
            };

            // Notify each invited company
            foreach (var companyId in message.InvitedCompanyIds)
            {
                var groupName = $"company-{companyId}";
                await notificationService.SendNotificationToGroupAsync(
                    groupName,
                    "Quotation Request Cancelled",
                    $"The quotation request you were invited to has been cancelled. Reason: {reason}",
                    NotificationType.WorkflowTransition,
                    metadata: metadata);
            }

            // Notify RM if available
            if (message.RmUserId.HasValue)
            {
                await notificationService.SendNotificationToUserAsync(
                    message.RmUserId.Value.ToString(),
                    "Quotation Request Cancelled",
                    $"The quotation request for your appraisal has been cancelled by the admin. Reason: {reason}",
                    NotificationType.WorkflowTransition,
                    metadata: metadata);
            }

            logger.LogInformation(
                "Sent QuotationCancelled notifications for QuotationRequestId={QuotationRequestId}, CompaniesNotified={Count}",
                message.QuotationRequestId, message.InvitedCompanyIds.Length);

            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error processing QuotationCancelled notification for QuotationRequestId={QuotationRequestId}",
                message.QuotationRequestId);
            throw;
        }
    }
}
