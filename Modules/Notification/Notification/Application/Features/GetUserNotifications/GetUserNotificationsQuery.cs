namespace Notification.Domain.Notifications.Features.GetUserNotifications;

public record GetUserNotificationsQuery(
    string UserId,
    bool UnreadOnly = false
) : IQuery<GetUserNotificationsResponse>;