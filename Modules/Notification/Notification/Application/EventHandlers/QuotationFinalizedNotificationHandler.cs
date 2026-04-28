using MassTransit;
using Notification.Data;
using Notification.Domain.Notifications.Models;
using Notification.Domain.Notifications.Services;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Notification.Domain.Notifications.EventHandlers;

/// <summary>
/// Notifies the winning external company and the RM that a quotation has been finalized.
/// </summary>
public class QuotationFinalizedNotificationHandler(
    INotificationService notificationService,
    ILogger<QuotationFinalizedNotificationHandler> logger,
    InboxGuard<NotificationDbContext> inboxGuard) : IConsumer<QuotationFinalizedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<QuotationFinalizedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;

        logger.LogInformation(
            "Processing QuotationFinalized notification for QuotationRequestId={QuotationRequestId}, WinningCompanyId={WinningCompanyId}",
            message.QuotationRequestId, message.WinningCompanyId);

        try
        {
            var metadata = new Dictionary<string, object>
            {
                { "quotationRequestId", message.QuotationRequestId },
                { "appraisalId", message.AppraisalId },
                { "winningCompanyId", message.WinningCompanyId },
                { "finalFeeAmount", message.FinalFeeAmount }
            };

            // Notify winning company
            var companyGroupName = $"company-{message.WinningCompanyId}";
            await notificationService.SendNotificationToGroupAsync(
                companyGroupName,
                "Quotation Awarded",
                $"Congratulations! Your quotation has been finalized with a fee of {message.FinalFeeAmount:N2} THB. An appraisal assignment will be created for you.",
                NotificationType.WorkflowTransition,
                metadata: metadata);

            // Notify RM if available
            if (message.RmUserId.HasValue)
            {
                await notificationService.SendNotificationToUserAsync(
                    message.RmUserId.Value.ToString(),
                    "Quotation Finalized",
                    "The appraisal quotation has been finalized. An external appraisal assignment has been created.",
                    NotificationType.WorkflowTransition,
                    metadata: metadata);
            }

            logger.LogInformation(
                "Sent QuotationFinalized notifications for QuotationRequestId={QuotationRequestId}",
                message.QuotationRequestId);

            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error processing QuotationFinalized notification for QuotationRequestId={QuotationRequestId}",
                message.QuotationRequestId);
            throw;
        }
    }
}
