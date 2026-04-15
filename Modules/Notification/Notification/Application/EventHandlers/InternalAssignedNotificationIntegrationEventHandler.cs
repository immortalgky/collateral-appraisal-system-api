using MassTransit;
using Notification.Data;
using Notification.Domain.Notifications.Models;
using Notification.Domain.Notifications.Services;
using Shared.Messaging.Filters;

namespace Notification.Domain.Notifications.EventHandlers;

public class InternalAssignedNotificationIntegrationEventHandler(
    INotificationService notificationService,
    ILogger<InternalAssignedNotificationIntegrationEventHandler> logger,
    InboxGuard<NotificationDbContext> inboxGuard) : IConsumer<InternalAssignedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<InternalAssignedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;

        logger.LogInformation(
            "Processing InternalAssigned notification for AppraisalId {AppraisalId}, CompletedBy {CompletedBy}",
            message.AppraisalId, message.CompletedBy);

        try
        {
            if (string.IsNullOrEmpty(message.CompletedBy))
            {
                logger.LogWarning(
                    "InternalAssigned event has no CompletedBy for AppraisalId {AppraisalId}. Skipping checker notification.",
                    message.AppraisalId);
                await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
                return;
            }

            var appraisalNumber = message.AppraisalNumber ?? "N/A";

            await notificationService.SendNotificationToUserAsync(
                message.CompletedBy,
                $"Appraisal Assigned: #{appraisalNumber}",
                $"Appraisal #{appraisalNumber} has been assigned internally to {message.AssigneeUserId}.",
                NotificationType.WorkflowTransition,
                metadata: new Dictionary<string, object>
                {
                    { "appraisalId", message.AppraisalId },
                    { "appraisalNumber", appraisalNumber },
                    { "assigneeUserId", message.AssigneeUserId },
                    { "assignmentMethod", message.AssignmentMethod }
                });

            logger.LogInformation(
                "Sent InternalAssigned notification to checker {CompletedBy} for appraisal {AppraisalNumber}",
                message.CompletedBy, appraisalNumber);

            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error processing InternalAssigned notification for AppraisalId {AppraisalId}",
                message.AppraisalId);
            throw;
        }
    }
}
