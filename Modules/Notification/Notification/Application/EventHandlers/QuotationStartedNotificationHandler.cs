using Auth.Contracts.Users;
using MassTransit;
using Notification.Data;
using Notification.Domain.Notifications.Models;
using Notification.Domain.Notifications.Services;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Notification.Domain.Notifications.EventHandlers;

/// <summary>
/// Notifies each ExtAdmin user of every invited external company that a new quotation
/// request has been sent, delivered as a persisted per-user notification (bell).
/// </summary>
public class QuotationStartedNotificationHandler(
    INotificationService notificationService,
    IUserLookupService userLookupService,
    ILogger<QuotationStartedNotificationHandler> logger,
    InboxGuard<NotificationDbContext> inboxGuard) : IConsumer<QuotationStartedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<QuotationStartedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;
        var ct = context.CancellationToken;

        logger.LogInformation(
            "Processing QuotationStarted notification for QuotationRequestId={QuotationRequestId}, InvitedCompanies={Count}",
            message.QuotationRequestId, message.InvitedCompanyIds.Length);

        try
        {
            var metadata = new Dictionary<string, object>
            {
                { "quotationRequestId", message.QuotationRequestId },
                { "appraisalId", message.AppraisalId },
                { "dueDate", message.CutOffTime }
            };

            foreach (var companyId in message.InvitedCompanyIds)
            {
                var usernames = await userLookupService.GetUsernamesInRoleAsync("ExtAdmin", companyId, ct);

                foreach (var username in usernames)
                {
                    await notificationService.SendNotificationToUserAsync(
                        username,
                        "New Quotation Request",
                        "A quotation request has been sent to your company. Please review and submit your bid.",
                        NotificationType.WorkflowTransition,
                        metadata: metadata);
                }

                if (usernames.Length == 0)
                    logger.LogWarning(
                        "No ExtAdmin users found for CompanyId={CompanyId} on QuotationRequestId={QuotationRequestId}",
                        companyId, message.QuotationRequestId);
            }

            logger.LogInformation(
                "Sent QuotationStarted per-user notifications for {Count} invited companies, QuotationRequestId={QuotationRequestId}",
                message.InvitedCompanyIds.Length, message.QuotationRequestId);

            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
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
