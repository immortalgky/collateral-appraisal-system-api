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
    public Task Consume(ConsumeContext<NotifyAssignment> context) =>
        inboxGuard.RunOnceAsync(context.MessageId, GetType().Name, _ => HandleAsync(context), context.CancellationToken);

    private async Task HandleAsync(ConsumeContext<NotifyAssignment> context)
    {
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
    }
}