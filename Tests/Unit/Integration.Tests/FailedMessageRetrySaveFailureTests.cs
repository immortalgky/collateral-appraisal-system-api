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
using Shared.Messaging.Services;
using Shared.Time;

namespace Integration.Tests;

/// <summary>
/// After a CONFIRMED publish, a failure saving <see cref="FailedMessage.MarkRetried"/>
/// must NEVER clear <see cref="FailedMessage.RetryClaimedAt"/> — the broker already has the message, so
/// losing the claim would let it be republished again. Drives
/// <see cref="FailedMessageCollectorService.SaveMarkRetriedWithRetryAsync"/> directly against an EF Core
/// InMemory database with a DbContext subclass whose SaveChangesAsync is rigged to fail — the publish
/// itself isn't this method's concern (by the time it's called, the broker already has the message).
/// </summary>
public class FailedMessageRetrySaveFailureTests
{
    private sealed class AlwaysThrowsOnSaveDbContext(DbContextOptions<IntegrationDbContext> options)
        : IntegrationDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("simulated DB failure saving MarkRetried");
    }

    private sealed class ThrowsOnceThenSucceedsDbContext(DbContextOptions<IntegrationDbContext> options)
        : IntegrationDbContext(options)
    {
        private int _saveCount;

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            _saveCount++;
            if (_saveCount == 1)
                throw new InvalidOperationException("simulated transient DB failure saving MarkRetried");

            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private static FailedMessageCollectorService CreateCollector(IDateTimeProvider dateTimeProvider)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:Host"] = "amqp://localhost:5672/",
                ["RabbitMQ:Username"] = "guest",
                ["RabbitMQ:Password"] = "guest",
            })
            .Build();

        return new FailedMessageCollectorService(
            Substitute.For<IServiceScopeFactory>(),
            Substitute.For<IHttpClientFactory>(),
            Substitute.For<ILogger<FailedMessageCollectorService>>(),
            dateTimeProvider,
            Options.Create(new FailedMessagesOptions()),
            configuration,
            new ReceiveEndpointDiscoveryObserver());
    }

    private static FailedMessage SeedClaimedRetryRequestedRow(
        string dbName, DateTime faultedAt, DateTime claimedAt)
    {
        var options = new DbContextOptionsBuilder<IntegrationDbContext>().UseInMemoryDatabase(dbName).Options;
        using var db = new IntegrationDbContext(options);

        var message = FailedMessage.Create(
            "APP-NODE-01", "appraisal-sync", FailedMessageKind.Error, Guid.NewGuid(), null, "Test.FakeEvent",
            null, "System.TimeoutException", "boom", null, 0, faultedAt, faultedAt, null, null, null,
            "{}"u8.ToArray(), "application/json", null);
        message.RequestRetry("test-actor", null, faultedAt);
        db.FailedMessages.Add(message);
        db.SaveChanges();

        // Simulate the collector's own claim UPDATE (ExecuteUpdateAsync, not supported by InMemory) —
        // reflection is the smallest way to seed "the collector already claimed this row" for the test.
        typeof(FailedMessage).GetProperty(nameof(FailedMessage.RetryClaimedAt))!.SetValue(message, claimedAt);
        db.SaveChanges();

        return message;
    }

    [Fact]
    public async Task SaveMarkRetriedWithRetryAsync_SaveAlwaysFails_LeavesClaimInPlace_StaysRetryRequested()
    {
        var dbName = Guid.NewGuid().ToString();
        var faultedAt = new DateTime(2026, 1, 1, 9, 0, 0);
        var claimedAt = faultedAt.AddMinutes(1);
        var seeded = SeedClaimedRetryRequestedRow(dbName, faultedAt, claimedAt);

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(claimedAt.AddSeconds(5));
        var collector = CreateCollector(dateTimeProvider);

        var options = new DbContextOptionsBuilder<IntegrationDbContext>().UseInMemoryDatabase(dbName).Options;
        await using var throwingDb = new AlwaysThrowsOnSaveDbContext(options);
        var tracked = await throwingDb.FailedMessages.SingleAsync(m => m.Id == seeded.Id, TestContext.Current.CancellationToken);

        await collector.SaveMarkRetriedWithRetryAsync(throwingDb, tracked, TestContext.Current.CancellationToken);

        // Re-read via a PLAIN context against the same InMemory database — the tracked entity's own
        // in-memory properties were already mutated by MarkRetried() before the throwing save; only the
        // actually-persisted row proves nothing committed.
        await using var verifyDb = new IntegrationDbContext(options);
        var reloaded = await verifyDb.FailedMessages.AsNoTracking().SingleAsync(m => m.Id == seeded.Id, TestContext.Current.CancellationToken);

        reloaded.Status.Should().Be(FailedMessageStatus.RetryRequested);
        reloaded.RetryClaimedAt.Should().Be(claimedAt, "a failed save must never clear the claim");
    }

    [Fact]
    public async Task SaveMarkRetriedWithRetryAsync_SaveFailsOnceThenSucceeds_ClearsClaim_MovesToRetried()
    {
        var dbName = Guid.NewGuid().ToString();
        var faultedAt = new DateTime(2026, 1, 1, 9, 0, 0);
        var claimedAt = faultedAt.AddMinutes(1);
        var seeded = SeedClaimedRetryRequestedRow(dbName, faultedAt, claimedAt);

        var now = claimedAt.AddSeconds(5);
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(now);
        var collector = CreateCollector(dateTimeProvider);

        var options = new DbContextOptionsBuilder<IntegrationDbContext>().UseInMemoryDatabase(dbName).Options;
        await using var flakyDb = new ThrowsOnceThenSucceedsDbContext(options);
        var tracked = await flakyDb.FailedMessages.SingleAsync(m => m.Id == seeded.Id, TestContext.Current.CancellationToken);

        await collector.SaveMarkRetriedWithRetryAsync(flakyDb, tracked, TestContext.Current.CancellationToken);

        await using var verifyDb = new IntegrationDbContext(options);
        var reloaded = await verifyDb.FailedMessages.AsNoTracking().SingleAsync(m => m.Id == seeded.Id, TestContext.Current.CancellationToken);

        reloaded.Status.Should().Be(FailedMessageStatus.Retried);
        reloaded.ActionAt.Should().Be(now);
        reloaded.RetryClaimedAt.Should().BeNull();
    }
}
