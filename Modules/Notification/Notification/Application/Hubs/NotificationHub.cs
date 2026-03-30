using Microsoft.AspNetCore.SignalR;

namespace Notification.Domain.Notifications.Hubs;

public class NotificationHub : Hub
{
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    public override async Task OnConnectedAsync()
    {
        // if (Context.User?.Identity?.IsAuthenticated == true)
        // {
        var username = Context.User.Identity.Name;
        if (!string.IsNullOrEmpty(username))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{username}");
        }
        //}

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // if (Context.User?.Identity?.IsAuthenticated == true)
        // {
        var username = Context.User.Identity.Name;
        if (!string.IsNullOrEmpty(username))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{username}");
        }
        //}

        await base.OnDisconnectedAsync(exception);
    }
}