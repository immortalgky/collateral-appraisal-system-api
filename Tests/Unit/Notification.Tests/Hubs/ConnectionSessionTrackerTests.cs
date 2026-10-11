using Microsoft.AspNetCore.SignalR;
using Notification.Domain.Notifications.Hubs;
using NSubstitute;

namespace Notification.Tests.Hubs;

public class ConnectionSessionTrackerTests
{
    private static HubCallerContext Connection() => Substitute.For<HubCallerContext>();

    [Fact]
    public void Abort_closes_only_the_connections_of_the_ended_sessions()
    {
        var tracker = new ConnectionSessionTracker();
        var ended = Guid.NewGuid();
        var alive = Guid.NewGuid();
        var endedA = Connection();
        var endedB = Connection();
        var aliveContext = Connection();
        tracker.Add("a", ended, endedA);
        tracker.Add("b", ended, endedB); // two tabs of the same session
        tracker.Add("c", alive, aliveContext);

        var aborted = tracker.Abort(new HashSet<Guid> { ended });

        Assert.Equal(2, aborted);
        endedA.Received(1).Abort();
        endedB.Received(1).Abort();
        aliveContext.DidNotReceive().Abort();
    }

    [Fact]
    public void Disconnected_connections_are_forgotten()
    {
        var tracker = new ConnectionSessionTracker();
        var session = Guid.NewGuid();
        tracker.Add("a", session, Connection());

        tracker.Remove("a");

        Assert.Empty(tracker.SessionIds());
        Assert.Equal(0, tracker.Abort(new HashSet<Guid> { session }));
    }

    [Fact]
    public void Session_ids_are_distinct()
    {
        var tracker = new ConnectionSessionTracker();
        var session = Guid.NewGuid();
        tracker.Add("a", session, Connection());
        tracker.Add("b", session, Connection());

        Assert.Single(tracker.SessionIds());
    }
}
