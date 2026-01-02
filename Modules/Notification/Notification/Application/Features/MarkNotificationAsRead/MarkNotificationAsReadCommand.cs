namespace Notification.Domain.Notifications.Features.MarkNotificationAsRead;

public record MarkNotificationAsReadCommand(
    Guid? NotificationId,
    string? UserId
) : ICommand<MarkNotificationAsReadResponse>;