using Microsoft.AspNetCore.SignalR;
using Notification.Contracts.Realtime;
using Notification.Domain.Notifications.Hubs;

namespace Notification.Domain.Notifications.Services;

public class RealtimeNotifier(IHubContext<NotificationHub> hubContext) : IRealtimeNotifier
{
    public Task SendToGroupAsync(string groupName, string eventName, object payload, CancellationToken ct = default)
        => hubContext.Clients.Group(groupName).SendAsync(eventName, payload, ct);
}
