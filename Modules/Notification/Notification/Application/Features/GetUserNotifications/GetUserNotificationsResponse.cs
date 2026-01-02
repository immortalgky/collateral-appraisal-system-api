using Notification.Domain.Notifications.Dtos;

namespace Notification.Domain.Notifications.Features.GetUserNotifications;

public record GetUserNotificationsResponse(
    List<NotificationDto> Notifications
);