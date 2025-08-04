using Notification.Notification.Dtos;

namespace Notification.Notification.Features.GetUserNotifications;

public record GetUserNotificationsResponse(
    List<NotificationDto> Notifications
);