using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shared.Configurations;
using Shared.Data.Outbox;
using Shared.Messaging.Services;
using Shared.Time;

namespace Shared.Tests.Messaging;

/// <summary>
/// The per-poll orphan reset sits ahead of ProcessBatchAsync in the delivery loop. If its UPDATE keeps
/// failing (lock timeout, permission) it must not stop delivery for the module: a non-cancellation error
/// is logged as a Warning and swallowed (the throttle stays unarmed, so the next poll tries again), while
/// cancellation still propagates. EF Core InMemory has no <c>ExecuteSqlRawAsync</c>, so the inner reset
/// throws a real <see cref="InvalidOperationException"/> here without any mocking.
/// </summary>
public class IntegrationEventDeliveryServiceOrphanResetGuardTests
{
    public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.ApplyConfiguration(new IntegrationEventOutboxConfiguration());
    }

    private static (IntegrationEventDeliveryService<TestDbContext> Service, ILogger<IntegrationEventDeliveryService<TestDbContext>> Logger)
        NewService()
    {
        var logger = Substitute.For<ILogger<IntegrationEventDeliveryService<TestDbContext>>>();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.ApplicationNow.Returns(new DateTime(2026, 1, 1, 9, 0, 0));
        var service = new IntegrationEventDeliveryService<TestDbContext>(
            Substitute.For<IServiceScopeFactory>(), logger, clock,
            Options.Create(new BackgroundJobsOptions()), Substitute.For<IHostApplicationLifetime>());
        return (service, logger);
    }

    private static TestDbContext NewDb() =>
        new(new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task TryResetOrphanedProcessingAsync_ResetThrows_LogsWarningAndDoesNotThrow()
    {
        var (service, logger) = NewService();
        await using var db = NewDb();

        await service.Invoking(s => s.TryResetOrphanedProcessingAsync(db, CancellationToken.None))
            .Should().NotThrowAsync();

        logger.ReceivedCalls().Should().Contain(call =>
            call.GetMethodInfo().Name == nameof(ILogger.Log) && (LogLevel)call.GetArguments()[0]! == LogLevel.Warning);
    }

    [Fact]
    public async Task TryResetOrphanedProcessingAsync_TokenCancelled_StillPropagates()
    {
        var (service, _) = NewService();
        await using var db = NewDb();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await service.Invoking(s => s.TryResetOrphanedProcessingAsync(db, cts.Token))
            .Should().ThrowAsync<Exception>();
    }
}
