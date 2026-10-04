using Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Request.Infrastructure;
using Shared.Configurations;
using Shared.Data.Outbox;
using Shared.Messaging.Services;
using Shared.Time;

namespace Integration.FailedMessages;

/// <summary>
/// Exercises the REAL SQL behind <see cref="IntegrationEventDeliveryService{TDbContext}.ResetOrphanedProcessingAsync"/>
/// and <see cref="OutboxCleanupJob{TDbContext}"/> against a Testcontainers database, using the Request
/// module's own <c>IntegrationEventOutbox</c> table. EF Core InMemory can't run these (raw
/// <c>ExecuteSqlRawAsync</c>), so a real database is required — a unit test would only cover a copied
/// predicate, never the SQL itself.
/// </summary>
[Collection("Integration")]
public class OutboxOrphanResetAndCleanupTests(IntegrationTestFixture fixture)
{
    private IServiceScope CreateScope() => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    /// <summary>A substitute clock (not the real DI one — it can't be advanced) so the per-instance
    /// throttle can be exercised deterministically against a real database.</summary>
    private static IntegrationEventDeliveryService<RequestDbContext> CreateService(
        IServiceScope scope, IDateTimeProvider dateTimeProvider) =>
        new(
            scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            scope.ServiceProvider.GetRequiredService<ILogger<IntegrationEventDeliveryService<RequestDbContext>>>(),
            dateTimeProvider,
            scope.ServiceProvider.GetRequiredService<IOptions<BackgroundJobsOptions>>(),
            Substitute.For<IHostApplicationLifetime>());

    private static async Task<Guid> SeedRowAsync(
        RequestDbContext db, string status, DateTime occurredAt,
        DateTime? processedAt = null, DateTime? processingStartedAt = null)
    {
        var id = Guid.CreateVersion7();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO [request].[IntegrationEventOutbox]
                 (Id, EventType, Payload, Headers, CorrelationId, OccurredAt, ProcessedAt, Error, RetryCount, Status, ProcessingStartedAt)
             VALUES ({id}, {"Test.Fake.Event, Test"}, {"{}"}, {"{}"}, NULL, {occurredAt}, {processedAt}, NULL, 0, {status}, {processingStartedAt})
             """);
        return id;
    }

    private static async Task<(string Status, DateTime? ProcessingStartedAt)> ReadRowAsync(RequestDbContext db, Guid id)
    {
        var row = await db.Set<IntegrationEventOutboxMessage>().AsNoTracking()
            .Where(m => m.Id == id)
            .Select(m => new { m.Status, m.ProcessingStartedAt })
            .FirstAsync();
        return (row.Status.ToString(), row.ProcessingStartedAt);
    }

    [Fact]
    public async Task ResetOrphanedProcessingAsync_ResetsOldNonNullProcessing_LeavesNullAndOthersUntouched()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RequestDbContext>();
        var realClock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var now = realClock.ApplicationNow;

        var oldProcessing = await SeedRowAsync(db, "Processing", now, processingStartedAt: now - OutboxDeliveryPolicy.OrphanedProcessingThreshold - TimeSpan.FromSeconds(1));
        var recentProcessing = await SeedRowAsync(db, "Processing", now, processingStartedAt: now - TimeSpan.FromSeconds(1));
        // A NULL ProcessingStartedAt comes only from an old binary mid-rolling-deploy — the per-poll
        // reset leaves it alone entirely (old OccurredAt or not); only OutboxCleanupJob's daily reset
        // (its NULL-with-old-OccurredAt rule, the job's only Processing reset) recovers it — see the test below.
        var nullStartedProcessingOld = await SeedRowAsync(
            db, "Processing", now - OutboxDeliveryPolicy.OrphanedProcessingThreshold - TimeSpan.FromSeconds(1),
            processingStartedAt: null);
        var nullStartedProcessingRecent = await SeedRowAsync(db, "Processing", now, processingStartedAt: null);
        var pending = await SeedRowAsync(db, "Pending", now);
        var processed = await SeedRowAsync(db, "Processed", now, processedAt: now);
        var failed = await SeedRowAsync(db, "Failed", now);

        var service = CreateService(scope, realClock);

        await service.ResetOrphanedProcessingAsync(db, TestContext.Current.CancellationToken);

        using var verifyScope = CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<RequestDbContext>();

        var (oldStatus, oldStartedAt) = await ReadRowAsync(verifyDb, oldProcessing);
        Assert.Equal("Pending", oldStatus);
        Assert.Null(oldStartedAt);

        // NULL ProcessingStartedAt is now UNTOUCHED by the per-poll reset, regardless of OccurredAt age.
        var (nullOldStatus, _) = await ReadRowAsync(verifyDb, nullStartedProcessingOld);
        Assert.Equal("Processing", nullOldStatus);

        var (nullRecentStatus, _) = await ReadRowAsync(verifyDb, nullStartedProcessingRecent);
        Assert.Equal("Processing", nullRecentStatus);

        var (recentStatus, recentStartedAt) = await ReadRowAsync(verifyDb, recentProcessing);
        Assert.Equal("Processing", recentStatus);
        Assert.NotNull(recentStartedAt);

        Assert.Equal("Pending", (await ReadRowAsync(verifyDb, pending)).Status);
        Assert.Equal("Processed", (await ReadRowAsync(verifyDb, processed)).Status);
        Assert.Equal("Failed", (await ReadRowAsync(verifyDb, failed)).Status);
    }

    /// <summary>
    /// The per-poll reset is throttled to once a minute per service instance — a second orphaned row
    /// discovered inside that window must wait for either the next throttle-eligible poll or (as here)
    /// OutboxCleanupJob's own daily reset, never a second immediate UPDATE on the same instance.
    /// </summary>
    [Fact]
    public async Task ResetOrphanedProcessingAsync_ThrottledToOncePerMinutePerInstance()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RequestDbContext>();

        var now = new DateTime(2026, 1, 1, 9, 0, 0);
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        // Closure over the mutable `now` local — reassigning it below changes what the NEXT call sees,
        // the standard NSubstitute way to drive a moving fake clock across several calls.
        dateTimeProvider.ApplicationNow.Returns(_ => now);

        var service = CreateService(scope, dateTimeProvider);

        var first = await SeedRowAsync(
            db, "Processing", now, processingStartedAt: now - OutboxDeliveryPolicy.OrphanedProcessingThreshold - TimeSpan.FromSeconds(1));

        await service.ResetOrphanedProcessingAsync(db, TestContext.Current.CancellationToken);
        Assert.Equal("Pending", (await ReadRowAsync(db, first)).Status);

        // A second row becomes eligible, but the 1-minute throttle on THIS instance hasn't elapsed yet.
        var second = await SeedRowAsync(
            db, "Processing", now, processingStartedAt: now - OutboxDeliveryPolicy.OrphanedProcessingThreshold - TimeSpan.FromSeconds(1));

        await service.ResetOrphanedProcessingAsync(db, TestContext.Current.CancellationToken);
        Assert.Equal("Processing", (await ReadRowAsync(db, second)).Status);

        // Advance the clock past the throttle window — the next call resets it.
        now = now.AddMinutes(1).AddSeconds(1);
        await service.ResetOrphanedProcessingAsync(db, TestContext.Current.CancellationToken);
        Assert.Equal("Pending", (await ReadRowAsync(db, second)).Status);
    }

    /// <summary>
    /// The throttle must only arm AFTER the reset UPDATE actually
    /// succeeds — a failed attempt (here, an already-cancelled token) must not silently sit out the full
    /// throttle window on a reset that never happened. Proven by: the failing call leaves the row
    /// untouched AND doesn't stamp the throttle, so the very next call (same clock, not advanced) still
    /// performs the reset instead of skipping it.
    /// </summary>
    [Fact]
    public async Task ResetOrphanedProcessingAsync_UpdateThrows_DoesNotArmThrottle_NextCallStillResets()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RequestDbContext>();

        var now = new DateTime(2026, 1, 1, 9, 0, 0);
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(_ => now);

        var service = CreateService(scope, dateTimeProvider);

        var orphaned = await SeedRowAsync(
            db, "Processing", now, processingStartedAt: now - OutboxDeliveryPolicy.OrphanedProcessingThreshold - TimeSpan.FromSeconds(1));

        using var alreadyCancelled = new CancellationTokenSource();
        await alreadyCancelled.CancelAsync();

        // The UPDATE never runs — the already-cancelled token makes ExecuteSqlRawAsync throw before it
        // reaches the database.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ResetOrphanedProcessingAsync(db, alreadyCancelled.Token));
        Assert.Equal("Processing", (await ReadRowAsync(db, orphaned)).Status);

        // Same clock (throttle window has NOT elapsed) — if the failed attempt above had wrongly armed
        // the throttle, this call would skip the reset entirely and the row would stay Processing.
        await service.ResetOrphanedProcessingAsync(db, TestContext.Current.CancellationToken);
        Assert.Equal("Pending", (await ReadRowAsync(db, orphaned)).Status);
    }

    /// <summary>The daily job's ONLY Processing reset is the legacy rule: a NULL ProcessingStartedAt row
    /// (an old binary mid-rolling-deploy that never stamped the column) is reset on OccurredAt older than
    /// <see cref="OutboxDeliveryPolicy.NullProcessingStartedAtOrphanedThreshold"/> (1 hour). A NULL row
    /// younger than that must stay untouched. A NON-NULL ProcessingStartedAt is never touched here, however
    /// old — the lease-holding delivery service's own reset owns that case and respects the lease.</summary>
    [Fact]
    public async Task OutboxCleanupJob_ResetsNullStartedOnlyPastOneHour_DeletesOldFailedAndOldProcessed_KeepsRecentFailed()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RequestDbContext>();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var now = dateTimeProvider.ApplicationNow;

        var nullStartedOld = await SeedRowAsync(
            db, "Processing", now - OutboxDeliveryPolicy.NullProcessingStartedAtOrphanedThreshold - TimeSpan.FromSeconds(1),
            processingStartedAt: null);
        // Old enough for the delivery service's 5-minute rule, but NOT for the 1-hour NULL rule — must stay
        // Processing.
        var nullStartedTooRecentForNullRule = await SeedRowAsync(
            db, "Processing", now - OutboxDeliveryPolicy.OrphanedProcessingThreshold - TimeSpan.FromSeconds(1),
            processingStartedAt: null);
        var oldFailed = await SeedRowAsync(db, "Failed", occurredAt: now.AddDays(-91));
        var recentFailed = await SeedRowAsync(db, "Failed", occurredAt: now.AddDays(-30));
        var oldProcessed = await SeedRowAsync(db, "Processed", occurredAt: now.AddDays(-8), processedAt: now.AddDays(-8));
        var recentProcessed = await SeedRowAsync(db, "Processed", occurredAt: now.AddDays(-3), processedAt: now.AddDays(-3));

        var job = new OutboxCleanupJob<RequestDbContext>(
            db,
            scope.ServiceProvider.GetRequiredService<ILogger<OutboxCleanupJob<RequestDbContext>>>(),
            dateTimeProvider);

        await job.ExecuteAsync(TestContext.Current.CancellationToken);

        using var verifyScope = CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<RequestDbContext>();

        Assert.Equal("Pending", (await ReadRowAsync(verifyDb, nullStartedOld)).Status);
        Assert.Equal("Processing", (await ReadRowAsync(verifyDb, nullStartedTooRecentForNullRule)).Status);

        var remainingIds = await verifyDb.Set<IntegrationEventOutboxMessage>().AsNoTracking()
            .Select(m => m.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(oldFailed, remainingIds);
        Assert.Contains(recentFailed, remainingIds);
        Assert.DoesNotContain(oldProcessed, remainingIds);
        Assert.Contains(recentProcessed, remainingIds);
    }

    /// <summary>
    /// A slow-but-alive batch (e.g. 50 publishes x 7s, about 6 minutes) holds rows in Processing with a
    /// non-NULL ProcessingStartedAt older than 5 minutes while its lease is renewed. The daily job ignores
    /// the lease, so it must NOT reset such a row — the lease-holder's ResetOrphanedProcessingAsync owns the
    /// 5-minute rule. But it IS the backstop when that per-poll reset keeps failing (its failures are swallowed)
    /// or never runs: a claim older than <see cref="OutboxDeliveryPolicy.StuckProcessingBackstopThreshold"/>
    /// (1 hour, far beyond the 50 x 15s = 12.5 minute worst batch, and renewal keeps the lease) is reset.
    /// </summary>
    [Fact]
    public async Task OutboxCleanupJob_ResetsNonNullProcessingOnlyPastTheBackstop_NotAtFiveMinutes()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RequestDbContext>();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var now = dateTimeProvider.ApplicationNow;

        var claimedSixMinutesAgo = await SeedRowAsync(
            db, "Processing", now.AddMinutes(-7), processingStartedAt: now.AddMinutes(-6));
        var claimedTwoHoursAgo = await SeedRowAsync(
            db, "Processing", now.AddHours(-3), processingStartedAt: now.AddHours(-2));

        await new OutboxCleanupJob<RequestDbContext>(
                db,
                scope.ServiceProvider.GetRequiredService<ILogger<OutboxCleanupJob<RequestDbContext>>>(),
                dateTimeProvider)
            .ExecuteAsync(TestContext.Current.CancellationToken);

        using var verifyScope = CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<RequestDbContext>();

        var (sixStatus, sixStartedAt) = await ReadRowAsync(verifyDb, claimedSixMinutesAgo);
        Assert.Equal("Processing", sixStatus);
        Assert.NotNull(sixStartedAt);

        var (twoHourStatus, twoHourStartedAt) = await ReadRowAsync(verifyDb, claimedTwoHoursAgo);
        Assert.Equal("Pending", twoHourStatus);
        Assert.Null(twoHourStartedAt);
    }

    /// <summary>
    /// Failed rows are purged by their LAST activity — <c>COALESCE(ProcessingStartedAt, OccurredAt)</c> — not by
    /// <c>OccurredAt</c> alone, so a row an operator resends that fails again is not deleted that night just
    /// because it was created more than 90 days ago. A Failed row with no recorded claim (failed by an older
    /// binary) falls back to <c>OccurredAt</c>.
    /// </summary>
    [Fact]
    public async Task OutboxCleanupJob_PurgesFailedByLastClaim_FallingBackToOccurredAt()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RequestDbContext>();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var now = dateTimeProvider.ApplicationNow;
        var created = now.AddDays(-100);

        var failedAgainRecently = await SeedRowAsync(db, "Failed", created, processingStartedAt: now.AddDays(-1));
        var failedClaimAlsoOld = await SeedRowAsync(db, "Failed", created, processingStartedAt: now.AddDays(-91));
        var failedNoClaimOld = await SeedRowAsync(db, "Failed", created, processingStartedAt: null);
        var failedNoClaimRecent = await SeedRowAsync(db, "Failed", now.AddDays(-30), processingStartedAt: null);

        await new OutboxCleanupJob<RequestDbContext>(
                db,
                scope.ServiceProvider.GetRequiredService<ILogger<OutboxCleanupJob<RequestDbContext>>>(),
                dateTimeProvider)
            .ExecuteAsync(TestContext.Current.CancellationToken);

        using var verifyScope = CreateScope();
        var remaining = await verifyScope.ServiceProvider.GetRequiredService<RequestDbContext>()
            .Set<IntegrationEventOutboxMessage>().AsNoTracking()
            .Select(m => m.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Contains(failedAgainRecently, remaining);
        Assert.DoesNotContain(failedClaimAlsoOld, remaining);
        Assert.DoesNotContain(failedNoClaimOld, remaining);
        Assert.Contains(failedNoClaimRecent, remaining);
    }

    /// <summary>
    /// The whole chain on a real row: an old row that is claimed and then fails (as the delivery service does)
    /// must survive the nightly purge, because the claim is what the purge ages it by.
    /// </summary>
    [Fact]
    public async Task OutboxCleanupJob_KeepsAnOldRowThatWasJustClaimedAndFailedAgain()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RequestDbContext>();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var now = dateTimeProvider.ApplicationNow;

        var id = await SeedRowAsync(db, "Pending", now.AddDays(-100));
        var row = await db.Set<IntegrationEventOutboxMessage>().SingleAsync(m => m.Id == id, TestContext.Current.CancellationToken);
        row.MarkAsProcessing(now);
        row.MarkAsFailed("disallowed type");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await new OutboxCleanupJob<RequestDbContext>(
                db,
                scope.ServiceProvider.GetRequiredService<ILogger<OutboxCleanupJob<RequestDbContext>>>(),
                dateTimeProvider)
            .ExecuteAsync(TestContext.Current.CancellationToken);

        using var verifyScope = CreateScope();
        var status = await ReadRowAsync(verifyScope.ServiceProvider.GetRequiredService<RequestDbContext>(), id);
        Assert.Equal("Failed", status.Status);
        Assert.NotNull(status.ProcessingStartedAt);
    }
}
