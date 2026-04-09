using System.Security.Claims;
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
        var username = ResolveUsername();
        if (!string.IsNullOrEmpty(username))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{username}");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var username = ResolveUsername();
        if (!string.IsNullOrEmpty(username))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{username}");
        }

        await base.OnDisconnectedAsync(exception);
    }

    // NotificationService publishes to group "User_{username}" using the login string
    // (e.g. "sth.staff2"). The "name" claim is always present in the access token
    // (TokenService.GetDestinations forces it to both tokens) and equals ApplicationUser.UserName,
    // so it's the reliable primary source. ClaimTypes.Name is the fallback in case OpenIddict's
    // inbound claim mapping is ever enabled. preferred_username is last because it only ships
    // when the "profile" scope is requested.
    private string? ResolveUsername()
    {
        var user = Context.User;
        if (user?.Identity?.IsAuthenticated != true) return null;

        return user.FindFirst("name")?.Value
            ?? user.FindFirst(ClaimTypes.Name)?.Value
            ?? user.FindFirst("preferred_username")?.Value;
    }
}