using Microsoft.Extensions.Hosting;
using Shared.Identity;

namespace Notification.Domain.Notifications.Hubs;

/// <summary>
/// Closes the hub connections of browser sessions that have ended (logout, idle, revoke, deactivation).
/// SignalR authenticates once, at connect, so without this a revoked session keeps receiving
/// notifications until the socket drops on its own. Polling the session table — rather than reacting to
/// an in-process event — also covers a revoke made on the other node, which this node cannot hear.
/// </summary>
public sealed class SessionConnectionSweeper(
    ConnectionSessionTracker tracker,
    IServiceScopeFactory scopeFactory,
    ILogger<SessionConnectionSweeper> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var sessionIds = tracker.SessionIds();
                if (sessionIds.Count == 0) continue;

                await using var scope = scopeFactory.CreateAsyncScope();
                var reader = scope.ServiceProvider.GetService<ISessionStatusReader>();
                if (reader is null) continue;

                var ended = await reader.GetEndedAsync(sessionIds, stoppingToken);
                if (ended.Count > 0)
                    logger.LogInformation("Closed {Count} notification connection(s) of ended sessions",
                        tracker.Abort(ended));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One failed sweep (database blip) must not stop the next.
                logger.LogWarning(ex, "Session connection sweep failed");
            }
        }
    }
}
