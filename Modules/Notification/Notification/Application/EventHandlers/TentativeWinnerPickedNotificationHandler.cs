using MassTransit;
using Notification.Data;
using Notification.Domain.Notifications.Models;
using Notification.Domain.Notifications.Services;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Notification.Domain.Notifications.EventHandlers;

/// <summary>
/// Notifies the admin pool and the winning company that a tentative winner has been picked.
/// </summary>
public class TentativeWinnerPickedNotificationHandler(
    INotificationService notificationService,
    ILogger<TentativeWinnerPickedNotificationHandler> logger,
    InboxGuard<NotificationDbContext> inboxGuard) : IConsumer<TentativeWinnerPickedIntegrationEvent>
{
    private const string AdminGroupName = "Admin";

    public async Task Consume(ConsumeContext<TentativeWinnerPickedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;

        logger.LogInformation(
            "Processing TentativeWinnerPicked notification for QuotationRequestId={QuotationRequestId}, CompanyId={CompanyId}, PickedBy={PickedBy}",
            message.QuotationRequestId, message.CompanyId, message.PickedBy);

        try
        {
            var metadata = new Dictionary<string, object>
            {
                { "quotationRequestId", message.QuotationRequestId },
                { "companyId", message.CompanyId },
                { "companyQuotationId", message.CompanyQuotationId },
                { "pickedBy", message.PickedBy },
                { "role", message.Role }
            };

            // Notify the admin pool (regardless of whether RM or Admin picked)
            await notificationService.SendNotificationToGroupAsync(
                AdminGroupName,
                "Tentative Winner Selected",
                $"A tentative winner has been selected by {message.Role}. Please proceed with negotiation or finalization.",
                NotificationType.WorkflowTransition,
                metadata: metadata);

            // Notify the winning company
            var companyGroupName = $"company-{message.CompanyId}";
            await notificationService.SendNotificationToGroupAsync(
                companyGroupName,
                "You Have Been Tentatively Selected",
                "Your quotation has been tentatively selected. The admin may contact you for negotiation.",
                NotificationType.WorkflowTransition,
                metadata: metadata);

            logger.LogInformation(
                "Sent TentativeWinnerPicked notifications for QuotationRequestId={QuotationRequestId}",
                message.QuotationRequestId);

            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error processing TentativeWinnerPicked notification for QuotationRequestId={QuotationRequestId}",
                message.QuotationRequestId);
            throw;
        }
    }
}
