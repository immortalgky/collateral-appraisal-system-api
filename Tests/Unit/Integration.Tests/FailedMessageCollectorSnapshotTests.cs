using FluentAssertions;
using Integration.Domain.FailedMessages;
using Integration.FailedMessages;
using Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using RabbitMQ.Client.Exceptions;
using Shared.Messaging.Services;
using Shared.Time;

namespace Integration.Tests;

/// <summary>
/// Snapshot must never present stale queue data as fresh: a round whose Management fetch did not succeed
/// stamps a new CollectedAt, so it must also drop the previous round's depths/consumer counts (the FE
/// already renders absent queue data as "—") instead of carrying them forward under the new timestamp.
/// And the AMQP-connection failure logging is de-duplicated like <c>RecordManagementStatus</c>: one
/// Error with the stack on the OK → failed transition, Information on recovery, Debug while it stays down.
/// </summary>
public class FailedMessageCollectorSnapshotTests
{
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, exception));
    }

    private static (FailedMessageCollectorService Collector, ServiceProvider Provider, DateTime[] Clock) Build(
        ILogger<FailedMessageCollectorService>? logger = null)
    {
        var clock = new[] { new DateTime(2026, 1, 1, 9, 0, 0) };
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(_ => clock[0]);

        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<IntegrationDbContext>(o => o.UseInMemoryDatabase(dbName));
        var provider = services.BuildServiceProvider();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:Host"] = "amqp://localhost:5672/",
                ["RabbitMQ:Username"] = "guest",
                ["RabbitMQ:Password"] = "guest",
            })
            .Build();

        var collector = new FailedMessageCollectorService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Substitute.For<IHttpClientFactory>(),
            logger ?? Substitute.For<ILogger<FailedMessageCollectorService>>(),
            dateTimeProvider,
            Options.Create(new FailedMessagesOptions()),
            configuration,
            new ReceiveEndpointDiscoveryObserver());

        return (collector, provider, clock);
    }

    [Fact]
    public async Task SnapshotAsync_FetchFailedAfterAGoodRound_DropsTheStaleQueueDataButStampsTheNewTime()
    {
        var (collector, provider, clock) = Build();
        await using var _ = provider;
        var ct = TestContext.Current.CancellationToken;

        const string goodJson = """[{"name":"appraisal-sync","ready":42,"unacked":1,"consumers":2,"samples":[1,2,3]}]""";
        await collector.SnapshotAsync(
            new FailedMessageCollectorService.ManagementQueuesFetch(
                BrokerManagementStatus.Ok, [], goodJson, null), ct);

        clock[0] = clock[0].AddMinutes(1);
        await collector.SnapshotAsync(
            new FailedMessageCollectorService.ManagementQueuesFetch(
                BrokerManagementStatus.Unreachable, null, null, "connection refused"), ct);

        using var scope = provider.CreateScope();
        var snapshot = await scope.ServiceProvider.GetRequiredService<IntegrationDbContext>()
            .BrokerSnapshots.SingleAsync(ct);
        snapshot.ManagementStatus.Should().Be(BrokerManagementStatus.Unreachable);
        snapshot.CollectedAt.Should().Be(clock[0]);
        snapshot.QueuesJson.Should().Be("[]",
            "the round fetched no queue data, so the previous depths must not ride along under a fresh CollectedAt");
    }

    [Fact]
    public async Task SnapshotAsync_NoFetchAtAll_WritesAnEmptyQueueList_ForANewAndAnExistingRow()
    {
        var (collector, provider, clock) = Build();
        await using var _ = provider;
        var ct = TestContext.Current.CancellationToken;

        // No fetch object (null) on the very first round: the NOT NULL column still gets "[]".
        await collector.SnapshotAsync(null, ct);

        // Then a good round, then another null fetch against the now-existing row.
        await collector.SnapshotAsync(
            new FailedMessageCollectorService.ManagementQueuesFetch(
                BrokerManagementStatus.Ok, [], """[{"name":"appraisal-sync","ready":42}]""", null), ct);
        clock[0] = clock[0].AddMinutes(1);
        await collector.SnapshotAsync(null, ct);

        using var scope = provider.CreateScope();
        var snapshot = await scope.ServiceProvider.GetRequiredService<IntegrationDbContext>()
            .BrokerSnapshots.SingleAsync(ct);
        snapshot.QueuesJson.Should().Be("[]");
        snapshot.CollectedAt.Should().Be(clock[0]);
    }

    [Fact]
    public async Task SnapshotAsync_FetchOk_StoresTheFreshQueueData()
    {
        var (collector, provider, _) = Build();
        await using var _ = provider;
        var ct = TestContext.Current.CancellationToken;

        const string goodJson = """[{"name":"appraisal-sync","ready":42}]""";
        await collector.SnapshotAsync(
            new FailedMessageCollectorService.ManagementQueuesFetch(
                BrokerManagementStatus.Ok, [], goodJson, null), ct);

        using var scope = provider.CreateScope();
        var snapshot = await scope.ServiceProvider.GetRequiredService<IntegrationDbContext>()
            .BrokerSnapshots.SingleAsync(ct);
        snapshot.QueuesJson.Should().Be(goodJson);
    }

    private static Exception Unreachable() => new BrokerUnreachableException(new IOException("connection refused"));

    [Fact]
    public void RecordAmqpConnectResult_StaysDown_LogsErrorWithStackOnlyOnce()
    {
        var logger = new CapturingLogger<FailedMessageCollectorService>();
        var (collector, provider, _) = Build(logger);
        using var _ = provider;

        var first = Unreachable();
        collector.RecordAmqpConnectResult(first);
        collector.RecordAmqpConnectResult(Unreachable());
        collector.RecordAmqpConnectResult(Unreachable());

        logger.Entries.Select(e => e.Level).Should().Equal(LogLevel.Error, LogLevel.Debug, LogLevel.Debug);
        logger.Entries[0].Exception.Should().BeSameAs(first, "the single Error keeps its stack");
        logger.Entries.Skip(1).Should().OnlyContain(e => e.Exception == null, "no stack while it stays down");
    }

    [Fact]
    public void RecordAmqpConnectResult_Recovers_LogsInformationOnce_ThenErrorAgainOnTheNextOutage()
    {
        var logger = new CapturingLogger<FailedMessageCollectorService>();
        var (collector, provider, _) = Build(logger);
        using var _ = provider;

        collector.RecordAmqpConnectResult(null); // healthy from the start: silent
        collector.RecordAmqpConnectResult(Unreachable());
        collector.RecordAmqpConnectResult(null);
        collector.RecordAmqpConnectResult(null); // still healthy: silent
        collector.RecordAmqpConnectResult(Unreachable());

        logger.Entries.Select(e => e.Level).Should().Equal(LogLevel.Error, LogLevel.Information, LogLevel.Error);
    }

    [Fact]
    public void LogStepFailure_ExceptionAlreadyRecordedAsAConnectFailure_IsNotLoggedAgain()
    {
        var logger = new CapturingLogger<FailedMessageCollectorService>();
        var (collector, provider, _) = Build(logger);
        using var _ = provider;

        var connectFailure = Unreachable();
        collector.RecordAmqpConnectResult(connectFailure);
        logger.Entries.Clear();

        collector.LogStepFailure("Collect", connectFailure);
        logger.Entries.Should().BeEmpty("the transition already logged it");

        var other = new InvalidOperationException("db blew up");
        collector.LogStepFailure("Collect", other);
        logger.Entries.Should().ContainSingle().Which.Should().Be((LogLevel.Error, (Exception?)other));
    }
}
