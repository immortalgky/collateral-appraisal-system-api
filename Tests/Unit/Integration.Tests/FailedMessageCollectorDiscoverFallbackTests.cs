using FluentAssertions;
using Integration.FailedMessages;
using MassTransit;
using Shared.Messaging.Services;

namespace Integration.Tests;

/// <summary>
/// The discover-fallback candidate list must include queues MassTransit actually opened on
/// this node, not just the 7 hand-maintained <see cref="OrderedEndpoints"/> — otherwise every other
/// consumer's <c>_error</c>/<c>_skipped</c> queue is never collected until the bank adds the management
/// tag (design D8).
/// </summary>
public class FailedMessageCollectorDiscoverFallbackTests
{
    /// <summary>Minimal fake — <see cref="ReceiveEndpointReady"/> is an interface with no public impl.</summary>
    private sealed class FakeReady(Uri inputAddress) : ReceiveEndpointReady
    {
        public Uri InputAddress { get; } = inputAddress;
        public IReceiveEndpoint ReceiveEndpoint => null!;
        public bool IsStarted => true;
    }

    [Fact]
    public async Task BuildFallbackCandidates_IncludesNonOrderedQueue_ObservedWithNamedVhost()
    {
        var observer = new ReceiveEndpointDiscoveryObserver();
        await observer.Ready(new FakeReady(new Uri("rabbitmq://host/my-vhost/appraisal-completed-webhook")));

        var candidates = FailedMessageCollectorService.BuildFallbackCandidates(observer.QueueNames);

        candidates.Should().Contain("appraisal-completed-webhook");
    }

    [Fact]
    public async Task BuildFallbackCandidates_IncludesNonOrderedQueue_ObservedWithDefaultVhost()
    {
        var observer = new ReceiveEndpointDiscoveryObserver();
        await observer.Ready(new FakeReady(new Uri("rabbitmq://host/appraisal-completed-webhook")));

        var candidates = FailedMessageCollectorService.BuildFallbackCandidates(observer.QueueNames);

        candidates.Should().Contain("appraisal-completed-webhook");
    }

    [Fact]
    public void BuildFallbackCandidates_AlwaysIncludesOrderedEndpoints_EvenWithNoObservations()
    {
        var candidates = FailedMessageCollectorService.BuildFallbackCandidates([]);

        candidates.Should().Contain(OrderedEndpoints.Names);
    }
}
