using MassTransit;
using Notification.Data;
using Notification.Domain.Notifications.Models;
using Notification.Domain.Notifications.Services;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Notification.Domain.Notifications.EventHandlers;

/// <summary>
/// Notifies each invited external company that a new quotation request has been sent to them.
/// </summary>
public class QuotationStartedNotificationHandler(
    INotificationService notificationService,
    ILogger<QuotationStartedNotificationHandler> logger,
    InboxGuard<NotificationDbContext> inboxGuard) : IConsumer<QuotationStartedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<QuotationStartedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;

        logger.LogInformation(
            "Processing QuotationStarted notification for QuotationRequestId={QuotationRequestId}, InvitedCompanies={Count}",
            message.QuotationRequestId, message.InvitedCompanyIds.Length);

        try
        {
            // Notify each invited company via its company-scoped group
            foreach (var companyId in message.InvitedCompanyIds)
            {
                var groupName = $"company-{companyId}";
                await notificationService.SendNotificationToGroupAsync(
                    groupName,
                    "New Quotation Request",
                    "You have been invited to submit a quotation. Please log in to review the details and submit your bid.",
                    NotificationType.WorkflowTransition,
                    metadata: new Dictionary<string, object>
                    {
                        { "quotationRequestId", message.QuotationRequestId },
                        { "appraisalId", message.AppraisalId },
                        { "dueDate", message.DueDate }
                    });
            }

            logger.LogInformation(
                "Sent QuotationStarted notifications to {Count} companies for QuotationRequestId={QuotationRequestId}",
                message.InvitedCompanyIds.Length, message.QuotationRequestId);

            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error processing QuotationStarted notification for QuotationRequestId={QuotationRequestId}",
                message.QuotationRequestId);
            throw;
        }
    }
}
