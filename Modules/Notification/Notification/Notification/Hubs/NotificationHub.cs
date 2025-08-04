using Microsoft.AspNetCore.SignalR;

namespace Notification.Notification.Hubs;

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
        var userId = Context.User.Identity.Name;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
        }
        //}

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // if (Context.User?.Identity?.IsAuthenticated == true)
        // {
        var userId = Context.User.Identity.Name;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{userId}");
        }
        //}

        await base.OnDisconnectedAsync(exception);
    }
}