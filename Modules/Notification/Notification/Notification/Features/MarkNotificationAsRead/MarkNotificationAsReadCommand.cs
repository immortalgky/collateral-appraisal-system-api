using Shared.Contracts.CQRS;

namespace Notification.Notification.Features.MarkNotificationAsRead;

public record MarkNotificationAsReadCommand(
    Guid? NotificationId,
    string? UserId
) : ICommand<MarkNotificationAsReadResponse>;