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
/// A message that gets retried and then faults again keeps the same
/// envelope, so it can land on the exact (MessageId, SourceQueue, Kind, FaultedAt) key as the ORIGINAL
/// row — which has since moved past Pending. The old behaviour acked on every dedup hit unconditionally,
/// silently dropping that second failure. These tests drive
/// <see cref="FailedMessageCollectorService.ResolveDuplicateAsync"/> directly against a real (InMemory)
/// <see cref="IntegrationDbContext"/> — the one helper both dedup call sites (unique-index violation,
/// <see cref="FailedMessageCollectorService.AlreadyCollectedAsync"/>) share — covering the Error kind
/// cheaply, without a broker or Testcontainers (the Skipped kind is covered end to end in
/// Tests/Integration/FailedMessages/FailedMessageCollectorE2ETests.cs, against a real RabbitMQ).
/// </summary>
public class FailedMessageDuplicateResolutionTests
{
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

    private static IntegrationDbContext CreateInMemoryDbContext() =>
        new(new DbContextOptionsBuilder<IntegrationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static FailedMessage CreateErrorMessage(
        Guid messageId, string sourceQueue, DateTime faultedAt, DateTime? collectedAt = null,
        Dictionary<string, string>? headers = null) =>
        FailedMessage.Create(
            "APP-NODE-01", sourceQueue, FailedMessageKind.Error, messageId, null, "Test.FakeEvent", null,
            "System.TimeoutException", "boom", null, 0, faultedAt, collectedAt ?? faultedAt, null, null,
            null, "{}"u8.ToArray(), "application/json", headers);

    private static FailedMessage CreateSkippedMessage(Guid messageId, string sourceQueue, DateTime faultedAt) =>
        FailedMessage.Create(
            "APP-NODE-01", sourceQueue, FailedMessageKind.Skipped, messageId, null, "Test.FakeEvent", null,
            FaultMessageParser.SkippedExceptionType, "No consumer", null, 0, faultedAt, faultedAt,
            null, null, null, "{}"u8.ToArray(), "application/json", null);

    private static async Task<FailedMessage> SeedAsync(
        IntegrationDbContext db, Func<FailedMessage> create, Action<FailedMessage>? advance = null)
    {
        var original = create();
        db.FailedMessages.Add(original);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        if (advance is not null)
        {
            advance(original);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return original;
    }

    [Fact]
    public async Task DedupHit_ExistingRowAlreadyRetried_InsertsNewRow_WithBumpedFaultedAt_KeepsOriginal()
    {
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        var faultedAt = new DateTime(2026, 1, 1, 9, 0, 0);
        var collectionTime = new DateTime(2026, 1, 1, 9, 5, 0);
        // ResolveDuplicateAsync must rebase to the entity's OWN CollectedAt (collectionTime below), NOT
        // a fresh clock read — deliberately distinct from collectionTime so a regression back to reading
        // the clock inside ResolveDuplicateAsync fails this test instead of coincidentally matching.
        var resolveCallTime = collectionTime.AddMilliseconds(50);
        dateTimeProvider.ApplicationNow.Returns(resolveCallTime);
        var collector = CreateCollector(dateTimeProvider);

        var messageId = Guid.NewGuid();
        const string sourceQueue = "appraisal-sync";

        await using var db = CreateInMemoryDbContext();
        var original = CreateErrorMessage(messageId, sourceQueue, faultedAt);
        db.FailedMessages.Add(original);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        original.RequestRetry("test-actor", null, faultedAt).Should().BeTrue();
        original.MarkRetried(faultedAt).Should().BeTrue();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // The retried message faulted again — same envelope, so the same MessageId/SourceQueue/Kind/
        // FaultedAt key as `original` (an InMemory dbContext doesn't enforce the unique index itself, so
        // this test drives the resolution step the way the real unique-violation catch/AlreadyCollectedAsync
        // callers do once they detect the hit). CollectedAt is stamped separately from (and later than)
        // FaultedAt, same as BuildEntity does in production — FaultedAt comes from the envelope, CollectedAt
        // from the clock at build time.
        var duplicate = CreateErrorMessage(messageId, sourceQueue, faultedAt, collectedAt: collectionTime);
        await collector.ResolveDuplicateAsync(db, duplicate, TestContext.Current.CancellationToken);

        var rows = await db.FailedMessages.AsNoTracking()
            .Where(m => m.MessageId == messageId && m.SourceQueue == sourceQueue)
            .ToListAsync(TestContext.Current.CancellationToken);

        rows.Should().HaveCount(2);
        rows.Should().ContainSingle(m => m.Id == original.Id && m.Status == FailedMessageStatus.Retried &&
                                          m.FaultedAt == faultedAt);
        // FaultedAt is rebased to the entity's OWN CollectedAt, not a fresh
        // clock read — the two must land on the exact same instant, not a few ms apart.
        rows.Should().ContainSingle(m => m.Id == duplicate.Id && m.Status == FailedMessageStatus.Pending &&
                                          m.FaultedAt == collectionTime && m.FaultedAt == m.CollectedAt);
    }

    [Fact]
    public async Task DedupHit_ExistingRowStillPending_DoesNotInsert()
    {
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        var faultedAt = new DateTime(2026, 1, 1, 9, 0, 0);
        dateTimeProvider.ApplicationNow.Returns(faultedAt.AddMinutes(5));
        var collector = CreateCollector(dateTimeProvider);

        var messageId = Guid.NewGuid();
        const string sourceQueue = "appraisal-sync";

        await using var db = CreateInMemoryDbContext();
        var original = CreateErrorMessage(messageId, sourceQueue, faultedAt);
        db.FailedMessages.Add(original);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Redelivery of the exact same unresolved fault (ack lost after the first insert) — original
        // row is still Pending, so this really is the same failure, not a new one.
        var redelivery = CreateErrorMessage(messageId, sourceQueue, faultedAt);
        await collector.ResolveDuplicateAsync(db, redelivery, TestContext.Current.CancellationToken);

        var count = await db.FailedMessages.CountAsync(TestContext.Current.CancellationToken);
        count.Should().Be(1);
    }

    private static Dictionary<string, string> FaultHeaders(string timestamp) => new()
    {
        ["MT-Fault-ExceptionType"] = "System.TimeoutException",
        ["MT-Fault-Timestamp"] = timestamp
    };

    /// <summary>
    /// The "Error collision = redelivery" shortcut is only sound when MT-Fault-Timestamp actually became the
    /// row's FaultedAt. If the header exists but does not parse, FaultedAt fell back to the envelope sentTime,
    /// which a retry preserves — so a collision with a resolved row can be a genuine re-failure and must be inserted.
    /// </summary>
    [Fact]
    public async Task DedupHit_ErrorWithUnparseableFaultTimestampHeader_ExistingRetried_InsertsNewRow()
    {
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        var faultedAt = new DateTime(2026, 1, 1, 9, 0, 0);
        var collectionTime = faultedAt.AddMinutes(5);
        var collector = CreateCollector(dateTimeProvider);
        var messageId = Guid.NewGuid();
        const string sourceQueue = "appraisal-sync";

        await using var db = CreateInMemoryDbContext();
        await SeedAsync(db, () => CreateErrorMessage(messageId, sourceQueue, faultedAt),
            m => { m.RequestRetry("test-actor", null, faultedAt); m.MarkRetried(faultedAt); });

        var duplicate = CreateErrorMessage(
            messageId, sourceQueue, faultedAt, collectionTime, FaultHeaders("not-a-timestamp"));
        await collector.ResolveDuplicateAsync(db, duplicate, TestContext.Current.CancellationToken);

        (await db.FailedMessages.AsNoTracking().CountAsync(TestContext.Current.CancellationToken)).Should().Be(2);
    }

    [Fact]
    public async Task DedupHit_ErrorWithParseableFaultTimestampHeader_ExistingRetried_StillSkipsAsRedelivery()
    {
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        var faultedAt = new DateTime(2026, 1, 1, 9, 0, 0);
        var collector = CreateCollector(dateTimeProvider);
        var messageId = Guid.NewGuid();
        const string sourceQueue = "appraisal-sync";

        await using var db = CreateInMemoryDbContext();
        await SeedAsync(db, () => CreateErrorMessage(messageId, sourceQueue, faultedAt),
            m => { m.RequestRetry("test-actor", null, faultedAt); m.MarkRetried(faultedAt); });

        var redelivery = CreateErrorMessage(
            messageId, sourceQueue, faultedAt, headers: FaultHeaders("2026-01-01T02:00:00.0000000Z"));
        var outcome = await collector.ResolveDuplicateAsync(db, redelivery, TestContext.Current.CancellationToken);

        outcome.Should().Be(FailedMessageCollectorService.PersistOutcome.Ack);
        (await db.FailedMessages.AsNoTracking().CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    /// <summary>
    /// A Skipped/Unparseable message colliding with a row that went RetryRequested only moments ago may be the
    /// retried message failing AGAIN (MarkRetried not saved yet, or a second collector instance). Acking would
    /// drop it, so it is nacked/requeued and reprocessed once the row has left RetryRequested.
    /// </summary>
    [Fact]
    public async Task DedupHit_SkippedCollidesWithYoungRetryRequestedRow_NackAndStop_NothingInserted()
    {
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        var faultedAt = new DateTime(2026, 1, 1, 9, 0, 0);
        var requestedAt = faultedAt.AddMinutes(1);
        dateTimeProvider.ApplicationNow.Returns(
            requestedAt + FailedMessageCollectorService.RetryRequestedNackWindow - TimeSpan.FromSeconds(1));
        var collector = CreateCollector(dateTimeProvider);
        var messageId = Guid.NewGuid();
        const string sourceQueue = "appraisal-sync";

        await using var db = CreateInMemoryDbContext();
        await SeedAsync(db, () => CreateSkippedMessage(messageId, sourceQueue, faultedAt),
            m => m.RequestRetry("test-actor", null, requestedAt));

        var outcome = await collector.SafeResolveDuplicateAsync(
            db, CreateSkippedMessage(messageId, sourceQueue, faultedAt), sourceQueue + "_skipped",
            TestContext.Current.CancellationToken);

        outcome.Should().Be(FailedMessageCollectorService.PersistOutcome.NackAndStop);
        (await db.FailedMessages.AsNoTracking().CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    /// <summary>
    /// A row stuck RetryRequested for longer than the window has a dead/abandoned owner node. Nacking forever
    /// would block that queue's head every round and starve NEW failures behind it, so the collision is treated
    /// like a Retried/Discarded one: the re-failure is inserted with a rebased FaultedAt and the message acked.
    /// </summary>
    [Fact]
    public async Task DedupHit_SkippedCollidesWithOldRetryRequestedRow_InsertsRebasedRow_Acked()
    {
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        var faultedAt = new DateTime(2026, 1, 1, 9, 0, 0);
        var requestedAt = faultedAt.AddMinutes(1);
        var now = requestedAt + FailedMessageCollectorService.RetryRequestedNackWindow + TimeSpan.FromSeconds(1);
        dateTimeProvider.ApplicationNow.Returns(now);
        var collector = CreateCollector(dateTimeProvider);
        var messageId = Guid.NewGuid();
        const string sourceQueue = "appraisal-sync";

        await using var db = CreateInMemoryDbContext();
        var original = await SeedAsync(db, () => CreateSkippedMessage(messageId, sourceQueue, faultedAt),
            m => m.RequestRetry("test-actor", null, requestedAt));

        var refailure = CreateSkippedMessage(messageId, sourceQueue, faultedAt);
        var outcome = await collector.SafeResolveDuplicateAsync(
            db, refailure, sourceQueue + "_skipped", TestContext.Current.CancellationToken);

        outcome.Should().Be(FailedMessageCollectorService.PersistOutcome.Ack);
        var rows = await db.FailedMessages.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
        rows.Should().HaveCount(2);
        rows.Should().ContainSingle(m => m.Id == original.Id && m.Status == FailedMessageStatus.RetryRequested);
        rows.Should().ContainSingle(m => m.Id == refailure.Id && m.Status == FailedMessageStatus.Pending &&
                                          m.FaultedAt == refailure.CollectedAt);
    }

    /// <summary>
    /// The window is measured from the most recent activity on the row — max(ActionAt, RetryClaimedAt) — not
    /// from the admin's request time. A collector that claimed the row a moment ago is still working on it even
    /// if the request itself is old (the node was down when the retry was requested).
    /// </summary>
    [Fact]
    public async Task DedupHit_OldActionAtButFreshRetryClaim_NackAndStop_NothingInserted()
    {
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        var faultedAt = new DateTime(2026, 1, 1, 9, 0, 0);
        var requestedAt = faultedAt.AddMinutes(1);
        var now = requestedAt + FailedMessageCollectorService.RetryRequestedNackWindow + TimeSpan.FromHours(1);
        dateTimeProvider.ApplicationNow.Returns(now);
        var collector = CreateCollector(dateTimeProvider);
        var messageId = Guid.NewGuid();
        const string sourceQueue = "appraisal-sync";

        await using var db = CreateInMemoryDbContext();
        var row = await SeedAsync(db, () => CreateSkippedMessage(messageId, sourceQueue, faultedAt),
            m => m.RequestRetry("test-actor", null, requestedAt));
        db.Entry(row).Property(m => m.RetryClaimedAt).CurrentValue = now.AddSeconds(-30);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var outcome = await collector.SafeResolveDuplicateAsync(
            db, CreateSkippedMessage(messageId, sourceQueue, faultedAt), sourceQueue + "_skipped",
            TestContext.Current.CancellationToken);

        outcome.Should().Be(FailedMessageCollectorService.PersistOutcome.NackAndStop);
        (await db.FailedMessages.AsNoTracking().CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task DedupHit_OldActionAtAndOldRetryClaim_InsertsRebasedRow_Acked()
    {
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        var faultedAt = new DateTime(2026, 1, 1, 9, 0, 0);
        var requestedAt = faultedAt.AddMinutes(1);
        var now = requestedAt + FailedMessageCollectorService.RetryRequestedNackWindow + TimeSpan.FromHours(1);
        dateTimeProvider.ApplicationNow.Returns(now);
        var collector = CreateCollector(dateTimeProvider);
        var messageId = Guid.NewGuid();
        const string sourceQueue = "appraisal-sync";

        await using var db = CreateInMemoryDbContext();
        var row = await SeedAsync(db, () => CreateSkippedMessage(messageId, sourceQueue, faultedAt),
            m => m.RequestRetry("test-actor", null, requestedAt));
        db.Entry(row).Property(m => m.RetryClaimedAt).CurrentValue =
            now - FailedMessageCollectorService.RetryRequestedNackWindow - TimeSpan.FromSeconds(1);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var outcome = await collector.SafeResolveDuplicateAsync(
            db, CreateSkippedMessage(messageId, sourceQueue, faultedAt), sourceQueue + "_skipped",
            TestContext.Current.CancellationToken);

        outcome.Should().Be(FailedMessageCollectorService.PersistOutcome.Ack);
        (await db.FailedMessages.AsNoTracking().CountAsync(TestContext.Current.CancellationToken)).Should().Be(2);
    }

    [Fact]
    public async Task DedupHit_SkippedCollidesWithPendingRow_StillAcked()
    {
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        var faultedAt = new DateTime(2026, 1, 1, 9, 0, 0);
        var collector = CreateCollector(dateTimeProvider);
        var messageId = Guid.NewGuid();
        const string sourceQueue = "appraisal-sync";

        await using var db = CreateInMemoryDbContext();
        await SeedAsync(db, () => CreateSkippedMessage(messageId, sourceQueue, faultedAt));

        var outcome = await collector.SafeResolveDuplicateAsync(
            db, CreateSkippedMessage(messageId, sourceQueue, faultedAt), sourceQueue + "_skipped",
            TestContext.Current.CancellationToken);

        outcome.Should().Be(FailedMessageCollectorService.PersistOutcome.Ack);
    }
}
