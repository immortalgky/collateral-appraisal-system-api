using Microsoft.AspNetCore.SignalR;
using Shared.Identity;

namespace Notification.Domain.Notifications.Hubs;

public class NotificationHub(ConnectionSessionTracker sessions) : Hub
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
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{username}");
        }

        // A browser session's id rides in the "sid" claim; remember it so the connection can be closed
        // when that session ends (see SessionConnectionSweeper). Bearer / dev callers carry none.
        if (Guid.TryParse(Context.User?.FindFirst("sid")?.Value, out var sessionId))
            sessions.Add(Context.ConnectionId, sessionId, Context);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        sessions.Remove(Context.ConnectionId);

        var username = ResolveUsername();
        if (!string.IsNullOrEmpty(username))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{username}");
        }

        await base.OnDisconnectedAsync(exception);
    }

    // NotificationService publishes to group "user-{username}" using the login string (e.g. "sth.staff2").
    // GetUserCode is the one place that knows which claim carries it, and in which order to look.
    private string? ResolveUsername() =>
        Context.User?.Identity?.IsAuthenticated == true ? Context.User.GetUserCode() : null;
}
