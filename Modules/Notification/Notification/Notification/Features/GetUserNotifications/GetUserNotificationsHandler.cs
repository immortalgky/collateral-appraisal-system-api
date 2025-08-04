using Notification.Notification.Services;
using Shared.Contracts.CQRS;

namespace Notification.Notification.Features.GetUserNotifications;

public class GetUserNotificationsHandler : IQueryHandler<GetUserNotificationsQuery, GetUserNotificationsResponse>
{
    private readonly INotificationService _notificationService;

    public GetUserNotificationsHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task<GetUserNotificationsResponse> Handle(GetUserNotificationsQuery request, CancellationToken cancellationToken)
    {
        var notifications = await _notificationService.GetUserNotificationsAsync(request.UserId, request.UnreadOnly);

        return new GetUserNotificationsResponse(notifications);
    }
}