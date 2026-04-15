using MassTransit;
using Notification.Data;
using Notification.Domain.Notifications.Models;
using Notification.Domain.Notifications.Services;
using Shared.Messaging.Filters;

namespace Notification.Domain.Notifications.EventHandlers;

public class AppraisalCreatedNotificationIntegrationEventHandler(
    INotificationService notificationService,
    ILogger<AppraisalCreatedNotificationIntegrationEventHandler> logger,
    InboxGuard<NotificationDbContext> inboxGuard) : IConsumer<AppraisalCreatedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AppraisalCreatedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;

        logger.LogInformation(
            "Processing AppraisalCreated notification for AppraisalId {AppraisalId}, RequestId {RequestId}",
            message.AppraisalId, message.RequestId);

        try
        {
            var notifyUser = message.RequestedBy ?? message.CreatedBy;

            if (string.IsNullOrEmpty(notifyUser))
            {
                logger.LogWarning(
                    "AppraisalCreated event has no RequestedBy/CreatedBy for AppraisalId {AppraisalId}. Skipping notification.",
                    message.AppraisalId);
                await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
                return;
            }

            var appraisalNumber = message.AppraisalNumber ?? "N/A";

            await notificationService.SendNotificationToUserAsync(
                notifyUser,
                $"Appraisal Created: #{appraisalNumber}",
                $"Your request has been processed. Appraisal #{appraisalNumber} has been created.",
                NotificationType.WorkflowTransition,
                metadata: new Dictionary<string, object>
                {
                    { "appraisalId", message.AppraisalId },
                    { "appraisalNumber", appraisalNumber },
                    { "requestId", message.RequestId }
                });

            logger.LogInformation(
                "Sent AppraisalCreated notification to {NotifyUser} for appraisal {AppraisalNumber}",
                notifyUser, appraisalNumber);

            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error processing AppraisalCreated notification for AppraisalId {AppraisalId}",
                message.AppraisalId);
            throw;
        }
    }
}
