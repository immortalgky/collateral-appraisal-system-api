namespace Notification.Notification.Features.MarkNotificationAsRead;

public record MarkNotificationAsReadResponse(
    bool Success,
    string Message
);