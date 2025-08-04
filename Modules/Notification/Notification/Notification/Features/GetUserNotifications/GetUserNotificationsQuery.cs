using Shared.Contracts.CQRS;

namespace Notification.Notification.Features.GetUserNotifications;

public record GetUserNotificationsQuery(
    string UserId,
    bool UnreadOnly = false
) : IQuery<GetUserNotificationsResponse>;