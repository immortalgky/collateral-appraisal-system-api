using MassTransit;
using Notification.Data;
using Notification.Domain.Notifications.Dtos;
using Notification.Domain.Notifications.Services;
using Shared.Messaging.Commands;
using Shared.Messaging.Filters;

namespace Notification.Domain.Notifications.CommandHandlers;

public class NotifyAssignmentCommandHandler(
    INotificationService notificationService,
    InboxGuard<NotificationDbContext> inboxGuard) : IConsumer<NotifyAssignment>
{
    public async Task Consume(ConsumeContext<NotifyAssignment> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var notification = new TaskAssignedNotificationDto(
            context.Message.CorrelationId,
            context.Message.TaskName,
            context.Message.AssignedTo,
            context.Message.AssignedType,
            "N/A",
            context.Message.TaskName,
            DateTime.Now,
            context.Message.NotifiedTo
        );
        await notificationService.SendTaskAssignedToOtherNotificationAsync(notification);

        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
    }
}