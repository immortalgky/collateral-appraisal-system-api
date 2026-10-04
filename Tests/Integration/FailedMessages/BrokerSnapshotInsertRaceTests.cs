using FluentAssertions;
using Integration.Domain.FailedMessages;
using Integration.FailedMessages;
using Integration.Fixtures;
using Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shared.Messaging.Services;
using Shared.Time;

namespace Integration.FailedMessages;

/// <summary>
/// Two processes with the same MachineName (IIS overlapped recycle) can both find no BrokerSnapshot row
/// for the node and both INSERT it. The loser hits the PK on real SQL Server (2627) — that is an expected
/// race, not an Error: the winner's row is just as good and the next round updates it. The "other
/// process" is simulated deterministically by an interceptor that inserts the node's row on a separate
/// connection after SnapshotAsync's find but before its INSERT executes.
/// </summary>
[Collection("Integration")]
public class BrokerSnapshotInsertRaceTests(IntegrationTestFixture fixture)
{
    private sealed class RivalInsertInterceptor(DbContextOptions<IntegrationDbContext> rivalOptions, string node)
        : SaveChangesInterceptor
    {
        private bool _done;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_done)
            {
                _done = true;
                await using var rival = new IntegrationDbContext(rivalOptions);
                rival.BrokerSnapshots.Add(BrokerSnapshot.Create(
                    node, new DateTime(2026, 1, 1, 9, 0, 0), BrokerManagementStatus.Ok, "[]", null));
                await rival.SaveChangesAsync(cancellationToken);
            }

            return result;
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogLevel> Levels { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Levels.Add(logLevel);
    }

    [Fact]
    public async Task SnapshotAsync_RivalProcessInsertsTheNodeRowFirst_IsSwallowedWithoutAnError()
    {
        var ct = TestContext.Current.CancellationToken;
        var node = Environment.MachineName;
        var plain = new DbContextOptionsBuilder<IntegrationDbContext>().UseSqlServer(fixture.ConnectionString).Options;

        // Start from a clean slate for this node (other collector tests may have left a row behind).
        await using (var cleanup = new IntegrationDbContext(plain))
            await cleanup.BrokerSnapshots.Where(s => s.Node == node).ExecuteDeleteAsync(ct);

        var racing = new DbContextOptionsBuilder<IntegrationDbContext>()
            .UseSqlServer(fixture.ConnectionString)
            .AddInterceptors(new RivalInsertInterceptor(plain, node))
            .Options;
        var services = new ServiceCollection();
        services.AddScoped(_ => new IntegrationDbContext(racing));
        await using var provider = services.BuildServiceProvider();

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(new DateTime(2026, 1, 1, 9, 0, 15));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:Host"] = "amqp://localhost:5672/",
                ["RabbitMQ:Username"] = "guest",
                ["RabbitMQ:Password"] = "guest",
            })
            .Build();
        var logger = new CapturingLogger<FailedMessageCollectorService>();
        var collector = new FailedMessageCollectorService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Substitute.For<IHttpClientFactory>(),
            logger,
            dateTimeProvider,
            Options.Create(new FailedMessagesOptions()),
            configuration,
            new ReceiveEndpointDiscoveryObserver());

        try
        {
            var act = () => collector.SnapshotAsync(
                new FailedMessageCollectorService.ManagementQueuesFetch(
                    BrokerManagementStatus.Ok, [], "[]", null), ct);

            await act.Should().NotThrowAsync("losing the insert race for the node row is expected, not a failure");
            logger.Levels.Should().NotContain(LogLevel.Error);

            await using var verify = new IntegrationDbContext(plain);
            (await verify.BrokerSnapshots.CountAsync(s => s.Node == node, ct)).Should().Be(1,
                "the rival's row stands; ours was dropped");
        }
        finally
        {
            await using var cleanup = new IntegrationDbContext(plain);
            await cleanup.BrokerSnapshots.Where(s => s.Node == node).ExecuteDeleteAsync(ct);
        }
    }
}
