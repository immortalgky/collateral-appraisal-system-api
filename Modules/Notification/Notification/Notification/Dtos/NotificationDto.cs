using Notification.Notification.Models;

namespace Notification.Notification.Dtos;

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