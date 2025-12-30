namespace Notification.Domain.Notifications.Features.MarkNotificationAsRead;

public record MarkNotificationAsReadResponse(
    bool Success,
    string Message
);