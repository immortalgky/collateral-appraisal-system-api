using Auth.Contracts.Users;
using MassTransit;
using Notification.Data;
using Notification.Domain.Notifications.Models;
using Notification.Domain.Notifications.Services;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Notification.Domain.Notifications.EventHandlers;

/// <summary>
/// Notifies all IntAdmin users that quotation submissions have closed and bids are ready for review.
/// Fires both when all companies respond and when the cut-off time passes (both paths publish this event
/// via CloseQuotationCommandHandler).
/// </summary>
public class QuotationSubmissionsClosedNotificationHandler(
    INotificationService notificationService,
    IUserLookupService userLookupService,
    ILogger<QuotationSubmissionsClosedNotificationHandler> logger,
    InboxGuard<NotificationDbContext> inboxGuard) : IConsumer<QuotationSubmissionsClosedIntegrationEvent>
{
    public Task Consume(ConsumeContext<QuotationSubmissionsClosedIntegrationEvent> context) =>
        inboxGuard.RunOnceAsync(context.MessageId, GetType().Name, _ => HandleAsync(context), context.CancellationToken);

    private async Task HandleAsync(ConsumeContext<QuotationSubmissionsClosedIntegrationEvent> context)
    {
        var message = context.Message;
        var ct = context.CancellationToken;

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

            var adminUsernames = await userLookupService.GetUsernamesInRoleAsync("IntAdmin", ct: ct);

            foreach (var username in adminUsernames)
            {
                await notificationService.SendNotificationToUserAsync(
                    username,
                    "Quotation Under Admin Review",
                    "Quotation submissions are ready — please review the bids and build a shortlist.",
                    NotificationType.WorkflowTransition,
                    metadata: metadata);
            }

            if (adminUsernames.Length == 0)
                logger.LogWarning(
                    "No IntAdmin users found to notify for QuotationRequestId={QuotationRequestId}",
                    message.QuotationRequestId);

            logger.LogInformation(
                "Sent QuotationSubmissionsClosed notification to {Count} IntAdmin user(s) for QuotationRequestId={QuotationRequestId}",
                adminUsernames.Length, message.QuotationRequestId);
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
