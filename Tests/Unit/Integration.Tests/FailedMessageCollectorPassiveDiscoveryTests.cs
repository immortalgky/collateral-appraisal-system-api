using FluentAssertions;
using Integration.FailedMessages;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using Shared.Messaging.Services;
using Shared.Time;

namespace Integration.Tests;

/// <summary>
/// Passive-declare discovery reuses ONE channel and reopens it after each broker NOT_FOUND
/// (which closes the channel). A sparse broker legitimately returns 404 for most candidates — the real
/// bus registers every consumer endpoint (~70 here, x2 for _error/_skipped) — so a round must not be cut
/// short by a reopen cap smaller than the candidate list: queues that exist later in the list were never
/// reached (E2E: Collector_RealDiscover_FindsNonOrderedQueue_ViaObservedReceiveEndpoint). Only a genuine
/// non-404 failure may stop the round.
/// </summary>
public class FailedMessageCollectorPassiveDiscoveryTests
{
    private sealed class FakeReady(Uri inputAddress) : ReceiveEndpointReady
    {
        public Uri InputAddress { get; } = inputAddress;
        public IReceiveEndpoint ReceiveEndpoint => null!;
        public bool IsStarted => true;
    }

    private sealed class WarningCounter : ILogger<FailedMessageCollectorService>
    {
        public int Warnings { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
                Warnings++;
        }
    }

    private static OperationInterruptedException Broker(ushort replyCode, string replyText) =>
        new(new ShutdownEventArgs(ShutdownInitiator.Peer, replyCode, replyText, cause: null!, CancellationToken.None));

    private static async Task<(FailedMessageCollectorService Collector, IConnection Connection, IChannel Channel)>
        BuildAsync(IEnumerable<string> observedEndpoints, Func<string, Task<QueueDeclareOk>> declare,
            Func<DateTime>? clock = null, ILogger<FailedMessageCollectorService>? logger = null)
    {
        var observer = new ReceiveEndpointDiscoveryObserver();
        foreach (var endpoint in observedEndpoints)
            await observer.Ready(new FakeReady(new Uri($"rabbitmq://host/{endpoint}")));

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(_ => clock?.Invoke() ?? new DateTime(2026, 1, 1, 9, 0, 0));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:Host"] = "amqp://localhost:5672/",
                ["RabbitMQ:Username"] = "guest",
                ["RabbitMQ:Password"] = "guest",
            })
            .Build();
        var collector = new FailedMessageCollectorService(
            Substitute.For<IServiceScopeFactory>(),
            Substitute.For<IHttpClientFactory>(),
            logger ?? Substitute.For<ILogger<FailedMessageCollectorService>>(),
            dateTimeProvider,
            Options.Create(new FailedMessagesOptions()),
            configuration,
            observer);

        var channel = Substitute.For<IChannel>();
        channel.QueueDeclarePassiveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => declare(call.ArgAt<string>(0)));
        var connection = Substitute.For<IConnection>();
        connection.CreateChannelAsync(Arg.Any<CreateChannelOptions?>(), Arg.Any<CancellationToken>())
            .Returns(channel);

        return (collector, connection, channel);
    }

    [Fact]
    public async Task Discover_ManyMissingQueuesAndOneExisting_ProbesEveryCandidateAndFindsIt()
    {
        // 60 endpoints (120 queues) that do not exist plus one that does — more 404s than any fixed
        // per-round reopen cap should be allowed to stop on. Observer key order is not guaranteed, so
        // assert that EVERY candidate was probed rather than relying on where "target" lands.
        var observed = Enumerable.Range(0, 60).Select(i => $"missing-{i}").Append("target").ToList();
        var probed = new List<string>();
        var (collector, connection, _) = await BuildAsync(observed, queue =>
        {
            probed.Add(queue);
            return queue == "target_error"
                ? Task.FromResult(new QueueDeclareOk(queue, 1, 0))
                : Task.FromException<QueueDeclareOk>(Broker(404, $"NOT_FOUND - no queue '{queue}'"));
        });

        var found = await collector.DiscoverViaPassiveDeclareAsync(connection, TestContext.Current.CancellationToken);

        found.Should().Equal(["target_error"]);
        probed.Should().HaveCount(FailedMessageCollectorService.BuildFallbackCandidates(observed).Count * 2);
    }

    [Fact]
    public async Task Discover_NonNotFoundFailure_StopsTheRoundWithoutReopening()
    {
        var (collector, connection, _) = await BuildAsync(["first"], queue =>
            queue == "first_error"
                ? Task.FromResult(new QueueDeclareOk(queue, 1, 0))
                : Task.FromException<QueueDeclareOk>(Broker(320, "CONNECTION_FORCED")));

        var found = await collector.DiscoverViaPassiveDeclareAsync(connection, TestContext.Current.CancellationToken);

        // What was found before the genuine failure is still returned, and no channel was reopened.
        found.Should().Equal(["first_error"]);
        await connection.Received(1).CreateChannelAsync(
            Arg.Any<CreateChannelOptions?>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(403, "ACCESS_REFUSED")]
    [InlineData(405, "RESOURCE_LOCKED")]
    public async Task Discover_SoftChannelErrorOnOneCandidate_SkipsItAndStillFindsTheNext(
        ushort replyCode, string replyText)
    {
        // The first candidate probed (other than the target) is refused/locked — the broker closes the CHANNEL,
        // not the connection. The old code ended the whole round there, so every candidate after it in the
        // stable order, including the target, stayed hidden forever. (Observer key order is not guaranteed, so
        // "first probed" is chosen at run time rather than hard-coding a name.)
        var logger = new WarningCounter();
        var now = new DateTime(2026, 1, 1, 9, 0, 0);
        var probes = new List<string>();
        string? refused = null;
        var observed = new[] { "a", "b", "c" };
        var (collector, connection, _) = await BuildAsync(observed, queue =>
        {
            probes.Add(queue);
            if (queue == "c_error")
                return Task.FromResult(new QueueDeclareOk(queue, 1, 0));
            refused ??= queue;
            return Task.FromException<QueueDeclareOk>(queue == refused
                ? Broker(replyCode, replyText)
                : Broker(404, $"NOT_FOUND - no queue '{queue}'"));
        }, () => now, logger);
        var ct = TestContext.Current.CancellationToken;

        (await collector.DiscoverViaPassiveDeclareAsync(connection, ct)).Should().Equal(["c_error"]);
        probes.Should().HaveCount(FailedMessageCollectorService.BuildFallbackCandidates(observed).Count * 2,
            "a soft error on one candidate must not end the round");
        logger.Warnings.Should().Be(1);
        // The channel the broker closed was replaced.
        await connection.Received().CreateChannelAsync(Arg.Any<CreateChannelOptions?>(), Arg.Any<CancellationToken>());

        // The refused queue is remembered for the recheck window: not re-probed, not warned about again.
        now = now.AddSeconds(30);
        probes.Clear();
        (await collector.DiscoverViaPassiveDeclareAsync(connection, ct)).Should().Equal(["c_error"]);
        probes.Should().NotContain(refused);
        logger.Warnings.Should().Be(1);

        // After the window it is probed again and warned about once more.
        now = now.Add(FailedMessageCollectorService.MissingQueueRecheck);
        probes.Clear();
        await collector.DiscoverViaPassiveDeclareAsync(connection, ct);
        probes.Should().Contain(refused);
        logger.Warnings.Should().Be(2);
    }

    [Fact]
    public async Task Discover_QueueMissingEarlierThenCreated_BecomesVisibleAfterTheShortRecheck()
    {
        // MassTransit creates <endpoint>_error only on the FIRST fault, so a brand-new failure must not
        // sit behind the 10-minute Management cooldown after an earlier 404 — missing fault queues get
        // their own 1-minute recheck.
        var now = new DateTime(2026, 1, 1, 9, 0, 0);
        var queueExists = false;
        var probes = 0;
        var (collector, connection, _) = await BuildAsync(["late"], queue =>
        {
            probes++;
            return queueExists && queue == "late_error"
                ? Task.FromResult(new QueueDeclareOk(queue, 1, 0))
                : Task.FromException<QueueDeclareOk>(Broker(404, $"NOT_FOUND - no queue '{queue}'"));
        }, () => now);
        var ct = TestContext.Current.CancellationToken;

        (await collector.DiscoverViaPassiveDeclareAsync(connection, ct)).Should().BeEmpty();
        var probesAfterFirstRound = probes;

        queueExists = true;
        now = now.AddSeconds(30);
        (await collector.DiscoverViaPassiveDeclareAsync(connection, ct)).Should().BeEmpty(
            "inside the recheck window the 404 is still trusted — no re-probe");
        probes.Should().Be(probesAfterFirstRound);

        now = now.AddSeconds(30).Add(TimeSpan.FromSeconds(1));
        (await collector.DiscoverViaPassiveDeclareAsync(connection, ct)).Should().Equal(["late_error"]);
        FailedMessageCollectorService.MissingQueueRecheck.Should().Be(TimeSpan.FromMinutes(1));
    }
}
