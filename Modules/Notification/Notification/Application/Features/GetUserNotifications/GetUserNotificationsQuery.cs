namespace Notification.Domain.Notifications.Features.GetUserNotifications;

public record GetUserNotificationsQuery(
    string Username,
    bool UnreadOnly = false
) : IQuery<GetUserNotificationsResponse>;