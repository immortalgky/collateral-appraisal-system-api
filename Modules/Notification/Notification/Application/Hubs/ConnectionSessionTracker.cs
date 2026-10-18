using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace Notification.Domain.Notifications.Hubs;

/// <summary>Which browser session each live hub connection on this node belongs to.</summary>
public sealed class ConnectionSessionTracker
{
    private readonly ConcurrentDictionary<string, (Guid SessionId, HubCallerContext Context)> _connections = new();

    public void Add(string connectionId, Guid sessionId, HubCallerContext context) =>
        _connections[connectionId] = (sessionId, context);

    public void Remove(string connectionId) => _connections.TryRemove(connectionId, out _);

    public IReadOnlyCollection<Guid> SessionIds() => _connections.Values.Select(c => c.SessionId).Distinct().ToList();

    /// <summary>Aborts every connection that belongs to one of the given sessions.</summary>
    public int Abort(IReadOnlySet<Guid> sessionIds)
    {
        var aborted = 0;
        foreach (var (_, connection) in _connections.Where(c => sessionIds.Contains(c.Value.SessionId)))
        {
            connection.Context.Abort();
            aborted++;
        }

        return aborted;
    }
}
