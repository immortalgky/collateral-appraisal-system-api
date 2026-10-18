using System.Collections.Concurrent;
using MassTransit;

namespace Shared.Messaging.Services;

/// <summary>
/// Singleton <see cref="IReceiveEndpointObserver"/> that records the queue name of every receive
/// endpoint this node hosts, as MassTransit reports it Ready. Feeds the Failed Messages collector's
/// discover-fallback (design §3 step 1, Modules/Integration/Integration/FailedMessages) so the fallback
/// candidate list covers every consumer this node actually opened — not just the manually maintained
/// <c>OrderedEndpoints</c> list — until the bank adds the RabbitMQ management tag (design D8).
///
/// Registration (Bootstrapper/Api/Program.cs, the MassTransit setup this app actually uses — the
/// commented-out <c>AddMassTransitWithAssemblies</c> in Shared.Messaging is dead code): register as a
/// plain singleton AND via <c>AddReceiveEndpointObserver</c> pointing at that same instance, so both the
/// collector (constructor injection of the concrete type) and MassTransit's own container wiring (which
/// only recognizes <see cref="IReceiveEndpointObserver"/>) resolve the identical object. Verified against
/// a real MassTransit 8.4.1 bus (in-memory transport, no broker needed): this exact registration shape
/// fires <see cref="Ready"/> and the same instance resolved from DI afterwards has every queue name.
/// </summary>
public sealed class ReceiveEndpointDiscoveryObserver : IReceiveEndpointObserver
{
    private readonly ConcurrentDictionary<string, byte> _queueNames = new();

    /// <summary>Every queue name observed Ready so far on this node.</summary>
    public IReadOnlyCollection<string> QueueNames => _queueNames.Keys.ToArray();

    public Task Ready(ReceiveEndpointReady ready)
    {
        var name = ExtractQueueName(ready.InputAddress);
        if (name is not null)
            _queueNames[name] = 0;

        return Task.CompletedTask;
    }

    public Task Stopping(ReceiveEndpointStopping stopping) => Task.CompletedTask;
    public Task Completed(ReceiveEndpointCompleted completed) => Task.CompletedTask;
    public Task Faulted(ReceiveEndpointFaulted faulted) => Task.CompletedTask;

    /// <summary>
    /// The queue name is always the last path segment of the InputAddress, regardless of vhost:
    /// verified with MassTransit's own RabbitMqEndpointAddress that the default vhost ("/") collapses
    /// into the host segment (<c>rabbitmq://host/queue</c>) while a named vhost adds one extra segment
    /// first (<c>rabbitmq://host/vhost/queue</c>) — in both cases the last segment is the queue name.
    /// <see cref="Uri.AbsolutePath"/> never includes the query string, so nothing else is needed to
    /// strip it.
    /// </summary>
    internal static string? ExtractQueueName(Uri inputAddress)
    {
        var path = inputAddress.AbsolutePath.TrimEnd('/');
        var lastSlash = path.LastIndexOf('/');
        var name = lastSlash >= 0 ? path[(lastSlash + 1)..] : path;
        return string.IsNullOrEmpty(name) ? null : name;
    }
}
