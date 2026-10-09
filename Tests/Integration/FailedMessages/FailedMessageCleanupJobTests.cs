using FluentAssertions;
using Integration.Domain.FailedMessages;
using Integration.Fixtures;
using Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shared.Time;

namespace Integration.FailedMessages;

/// <summary>
/// BrokerSnapshots are keyed by node and overwritten every round, so a decommissioned or renamed node's row
/// is never touched again. The daily cleanup job must age those out — against real SQL, because the delete is
/// raw SQL (EF InMemory can't run it).
/// </summary>
[Collection("Integration")]
public class FailedMessageCleanupJobTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task ExecuteAsync_DeletesBrokerSnapshotsNotCollectedForSevenDays_KeepsRecentOnes()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var decommissioned = $"OLD-{suffix}";
        var nearlyStale = $"EDGE-{suffix}";
        var live = $"LIVE-{suffix}";

        db.BrokerSnapshots.AddRange(
            BrokerSnapshot.Create(decommissioned, now.AddDays(-7).AddMinutes(-1), BrokerManagementStatus.Ok, "[]", null),
            BrokerSnapshot.Create(nearlyStale, now.AddDays(-7).AddMinutes(1), BrokerManagementStatus.Ok, "[]", null),
            BrokerSnapshot.Create(live, now, BrokerManagementStatus.Ok, "[]", null));
        await db.SaveChangesAsync(ct);

        await NewJob(db, now).ExecuteAsync(ct);

        var remaining = await db.BrokerSnapshots.AsNoTracking()
            .Where(s => s.Node == decommissioned || s.Node == nearlyStale || s.Node == live)
            .Select(s => s.Node)
            .ToListAsync(ct);
        remaining.Should().BeEquivalentTo([nearlyStale, live]);
    }

    // A Skipped message has no consumer, so Retry can't help; left Pending, a broken binding grows the table
    // forever. Only Kind=Skipped AND Status=Pending ages out here, by CollectedAt (when the row entered our table).
    [Fact]
    public async Task ExecuteAsync_DeletesSkippedPendingRowsOlderThanRetention_KeepsEverythingElse()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;

        var oldSkipped = await SeedAsync(db, FailedMessageKind.Skipped, now.AddDays(-31));
        var recentSkipped = await SeedAsync(db, FailedMessageKind.Skipped, now.AddDays(-29));
        var oldError = await SeedAsync(db, FailedMessageKind.Error, now.AddDays(-400));
        // Governed by the 90-day ActionAt rule (ActionAt = now here), not by this one.
        var oldSkippedDiscarded = await SeedAsync(
            db, FailedMessageKind.Skipped, now.AddDays(-31), discardedAt: now);

        await NewJob(db, now).ExecuteAsync(ct);

        var remaining = await db.FailedMessages.AsNoTracking()
            .Where(m => m.Id == oldSkipped || m.Id == recentSkipped || m.Id == oldError || m.Id == oldSkippedDiscarded)
            .Select(m => m.Id)
            .ToListAsync(ct);
        remaining.Should().BeEquivalentTo([recentSkipped, oldError, oldSkippedDiscarded]);
    }

    [Fact]
    public async Task ExecuteAsync_SkippedPendingRetention_ReadsTheConfiguredDays()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;

        var fifteenDays = await SeedAsync(db, FailedMessageKind.Skipped, now.AddDays(-15));
        var fiveDays = await SeedAsync(db, FailedMessageKind.Skipped, now.AddDays(-5));

        await NewJob(db, now, skippedPendingRetentionDays: 10).ExecuteAsync(ct);

        var remaining = await db.FailedMessages.AsNoTracking()
            .Where(m => m.Id == fifteenDays || m.Id == fiveDays)
            .Select(m => m.Id)
            .ToListAsync(ct);
        remaining.Should().BeEquivalentTo([fiveDays]);
    }

    // RevertRetry (a retry whose original queue is gone) puts a Skipped row back to Pending with ActionAt = now, so
    // the grace window restarts from the admin action: it must not be purged the night after being retried.
    [Fact]
    public async Task ExecuteAsync_SkippedPendingRowTouchedByAdminRecently_IsKeptDespiteOldCollectedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;

        var revertedRecently = await SeedAsync(
            db, FailedMessageKind.Skipped, now.AddDays(-31), revertedAt: now.AddDays(-1));
        var revertedLongAgo = await SeedAsync(
            db, FailedMessageKind.Skipped, now.AddDays(-60), revertedAt: now.AddDays(-31));

        await NewJob(db, now).ExecuteAsync(ct);

        var remaining = await db.FailedMessages.AsNoTracking()
            .Where(m => m.Id == revertedRecently || m.Id == revertedLongAgo)
            .Select(m => m.Id)
            .ToListAsync(ct);
        remaining.Should().BeEquivalentTo([revertedRecently]);
    }

    [Fact]
    public async Task ExecuteAsync_LogsOneLinePerSourceQueueAndMessageTypeBeforePurgingSkippedPendingRows()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var queueA = $"purge-a-{suffix}_skipped";
        var queueB = $"purge-b-{suffix}_skipped";

        await SeedAsync(db, FailedMessageKind.Skipped, now.AddDays(-40), sourceQueue: queueA, messageType: "Test.EventX");
        await SeedAsync(db, FailedMessageKind.Skipped, now.AddDays(-35), sourceQueue: queueA, messageType: "Test.EventX");
        await SeedAsync(db, FailedMessageKind.Skipped, now.AddDays(-32), sourceQueue: queueA, messageType: "Test.EventY");
        await SeedAsync(db, FailedMessageKind.Skipped, now.AddDays(-31), sourceQueue: queueB, messageType: "Test.EventX");
        // Not purged, so must not be logged as purged.
        await SeedAsync(db, FailedMessageKind.Skipped, now.AddDays(-5), sourceQueue: $"keep-{suffix}_skipped");

        var logger = new ListLogger<FailedMessageCleanupJob>();
        await NewJob(db, now, logger: logger).ExecuteAsync(ct);

        var purging = logger.Messages.Where(m => m.Contains("Purging") && m.Contains(suffix)).ToList();
        purging.Should().HaveCount(3);
        purging.Should().ContainSingle(m => m.Contains("Purging 2 pending skipped rows from " + queueA + " (Test.EventX)"));
        purging.Should().ContainSingle(m => m.Contains("Purging 1 pending skipped rows from " + queueA + " (Test.EventY)"));
        purging.Should().ContainSingle(m => m.Contains("Purging 1 pending skipped rows from " + queueB + " (Test.EventX)"));
        purging.Should().OnlyContain(m => m.Contains("older than 30 days"));
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }

    private static FailedMessageCleanupJob NewJob(
        IntegrationDbContext db, DateTime now, int skippedPendingRetentionDays = 30,
        ILogger<FailedMessageCleanupJob>? logger = null)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.ApplicationNow.Returns(now);
        return new FailedMessageCleanupJob(
            db, logger ?? NullLogger<FailedMessageCleanupJob>.Instance, clock,
            Options.Create(new FailedMessagesOptions { SkippedPendingRetentionDays = skippedPendingRetentionDays }));
    }

    /// <summary>FaultedAt is deliberately set far from CollectedAt to prove the rule keys on CollectedAt.</summary>
    private static async Task<Guid> SeedAsync(
        IntegrationDbContext db, string kind, DateTime collectedAt, DateTime? discardedAt = null,
        DateTime? revertedAt = null, string? sourceQueue = null, string messageType = "Test.FakeEvent")
    {
        var message = FailedMessage.Create(
            "CLEANUP-TEST",
            sourceQueue ?? $"cleanup-{Guid.NewGuid():N}_{(kind == FailedMessageKind.Skipped ? "skipped" : "error")}",
            kind, Guid.CreateVersion7(), null, messageType, null, "Skipped", "no consumer", null, 0,
            collectedAt.AddDays(-1000), collectedAt, null, null, null, [], "application/json", null);
        if (discardedAt is not null)
            message.Discard("seed-actor", "seeded as discarded", discardedAt.Value);
        if (revertedAt is not null)
        {
            message.RequestRetry("seed-actor", null, revertedAt.Value);
            message.RevertRetry("original queue gone", revertedAt.Value);
        }

        db.FailedMessages.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return message.Id;
    }
}
