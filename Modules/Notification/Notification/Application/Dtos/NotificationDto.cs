using Notification.Domain.Notifications.Models;

namespace Notification.Domain.Notifications.Dtos;

public record NotificationDto(
    Guid Id,
    string Title,
    string Message,
    NotificationType Type,
    DateTime CreatedAt,
    bool IsRead,
    string? ActionUrl,
    Dictionary<string, object>? Metadata
);