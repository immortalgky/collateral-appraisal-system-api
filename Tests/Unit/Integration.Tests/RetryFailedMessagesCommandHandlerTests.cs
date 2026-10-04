using FluentAssertions;
using Integration.Application.Features.FailedMessages;
using Integration.Application.Features.FailedMessages.DiscardFailedMessages;
using Integration.Application.Features.FailedMessages.RetryFailedMessages;
using Integration.Domain.FailedMessages;
using Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shared.Identity;
using Shared.Messaging.Filters;
using Shared.Time;

namespace Integration.Tests;

/// <summary>
/// A retry inside InboxGuard's stale-claim window is skipped as TooSoon rather than accepted and
/// silently no-op'd later; discard also accepts a RetryRequested row.
/// </summary>
public class RetryFailedMessagesCommandHandlerTests
{
    private static IntegrationDbContext CreateInMemoryDbContext() =>
        new(new DbContextOptionsBuilder<IntegrationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ICurrentUserService CurrentUser()
    {
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserCode.Returns("test-actor");
        return currentUser;
    }

    private static IDateTimeProvider FixedClock(DateTime now)
    {
        var provider = Substitute.For<IDateTimeProvider>();
        provider.ApplicationNow.Returns(now);
        return provider;
    }

    private static FailedMessage SeedPending(IntegrationDbContext db, DateTime faultedAt, DateTime? collectedAt = null)
    {
        var message = FailedMessage.Create(
            "APP-NODE-01", "appraisal-sync", FailedMessageKind.Error, Guid.NewGuid(), null, "Test.FakeEvent",
            null, "System.TimeoutException", "boom", null, 0, faultedAt, collectedAt ?? faultedAt, null, null, null,
            "{}"u8.ToArray(), "application/json", null);
        db.FailedMessages.Add(message);
        db.SaveChanges();
        return message;
    }

    [Fact]
    public async Task Retry_WithinStaleClaimWindow_IsSkippedAsTooSoon()
    {
        var faultedAt = new DateTime(2026, 1, 1, 9, 0, 0);
        var now = faultedAt + InboxGuardPolicy.StaleThreshold - TimeSpan.FromSeconds(1);

        await using var db = CreateInMemoryDbContext();
        var message = SeedPending(db, faultedAt);

        var handler = new RetryFailedMessagesCommandHandler(db, CurrentUser(), FixedClock(now));
        var result = await handler.Handle(
            new RetryFailedMessagesCommand([message.Id], null, null), CancellationToken.None);

        result.Accepted.Should().BeEmpty();
        result.Skipped.Should().ContainSingle(s => s.Id == message.Id && s.Reason == "TooSoon");
        // TooSoon omits by/at (api-contract.md) — unlike NotPending.
        result.Skipped.Single().By.Should().BeNull();
        result.Skipped.Single().At.Should().BeNull();
    }

    // An Error row whose MT-Fault-Timestamp header is missing or unparseable gets FaultedAt = the PUBLISH
    // time, which can be arbitrarily older than the real fault. CollectedAt is always on or after the real
    // fault, so the window is measured from max(FaultedAt, CollectedAt) — otherwise a retry passes TooSoon
    // while the consumer's inbox claim is still Processing and is swallowed as a duplicate.
    [Fact]
    public async Task Retry_FaultedAtOldButCollectedAtRecent_IsSkippedAsTooSoon()
    {
        var faultedAt = new DateTime(2026, 1, 1, 9, 0, 0);
        var collectedAt = faultedAt.AddHours(1);
        var now = collectedAt + InboxGuardPolicy.StaleThreshold - TimeSpan.FromSeconds(1);

        await using var db = CreateInMemoryDbContext();
        var message = SeedPending(db, faultedAt, collectedAt);

        var handler = new RetryFailedMessagesCommandHandler(db, CurrentUser(), FixedClock(now));
        var result = await handler.Handle(
            new RetryFailedMessagesCommand([message.Id], null, null), CancellationToken.None);

        result.Accepted.Should().BeEmpty();
        result.Skipped.Should().ContainSingle(s => s.Id == message.Id && s.Reason == "TooSoon");
    }

    [Fact]
    public async Task Retry_FaultedAtOldAndCollectedAtWindowElapsed_IsAccepted()
    {
        var faultedAt = new DateTime(2026, 1, 1, 9, 0, 0);
        var collectedAt = faultedAt.AddHours(1);
        var now = collectedAt + InboxGuardPolicy.StaleThreshold + TimeSpan.FromSeconds(1);

        await using var db = CreateInMemoryDbContext();
        var message = SeedPending(db, faultedAt, collectedAt);

        var handler = new RetryFailedMessagesCommandHandler(db, CurrentUser(), FixedClock(now));
        var result = await handler.Handle(
            new RetryFailedMessagesCommand([message.Id], null, null), CancellationToken.None);

        result.Accepted.Should().ContainSingle(id => id == message.Id);
    }

    [Fact]
    public async Task Retry_DuplicateIds_AreProcessedOnce_NotReportedAsBothAcceptedAndSkipped()
    {
        var faultedAt = new DateTime(2026, 1, 1, 9, 0, 0);
        var now = faultedAt + InboxGuardPolicy.StaleThreshold + TimeSpan.FromSeconds(1);

        await using var db = CreateInMemoryDbContext();
        var message = SeedPending(db, faultedAt);
        var unknown = Guid.NewGuid();

        var handler = new RetryFailedMessagesCommandHandler(db, CurrentUser(), FixedClock(now));
        var result = await handler.Handle(
            new RetryFailedMessagesCommand([message.Id, message.Id, unknown, unknown], null, null),
            CancellationToken.None);

        result.Accepted.Should().Equal(message.Id);
        result.Skipped.Should().ContainSingle().Which.Should().Match<SkippedFailedMessage>(
            s => s.Id == unknown && s.Reason == "NotFound");
    }

    [Fact]
    public async Task Retry_AfterStaleClaimWindowElapses_IsAccepted()
    {
        var faultedAt = new DateTime(2026, 1, 1, 9, 0, 0);
        var now = faultedAt + InboxGuardPolicy.StaleThreshold + TimeSpan.FromSeconds(1);

        await using var db = CreateInMemoryDbContext();
        var message = SeedPending(db, faultedAt);

        var handler = new RetryFailedMessagesCommandHandler(db, CurrentUser(), FixedClock(now));
        var result = await handler.Handle(
            new RetryFailedMessagesCommand([message.Id], null, null), CancellationToken.None);

        result.Accepted.Should().ContainSingle(id => id == message.Id);
        result.Skipped.Should().BeEmpty();
    }

    [Fact]
    public async Task Discard_RetryRequestedRow_IsAccepted()
    {
        var faultedAt = new DateTime(2026, 1, 1, 9, 0, 0);
        var now = faultedAt.AddMinutes(1);

        await using var db = CreateInMemoryDbContext();
        var message = SeedPending(db, faultedAt);
        message.RequestRetry("someone-else", null, faultedAt);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new DiscardFailedMessagesCommandHandler(db, CurrentUser(), FixedClock(now));
        var result = await handler.Handle(
            new DiscardFailedMessagesCommand([message.Id], "node is dead, clearing it", null),
            CancellationToken.None);

        result.Accepted.Should().ContainSingle(id => id == message.Id);
        var reloaded = await db.FailedMessages.AsNoTracking().SingleAsync(m => m.Id == message.Id, TestContext.Current.CancellationToken);
        reloaded.Status.Should().Be(FailedMessageStatus.Discarded);
    }

    /// <summary>Sets the private-setter <see cref="FailedMessage.RetryClaimedAt"/> via reflection — the
    /// collector claims it with a single conditional SQL UPDATE (ExecuteUpdateAsync, not supported by the
    /// InMemory provider these handler tests run against), so this is the smallest way to seed a "the
    /// collector is mid-publish" row for the discard handler's "Publishing" check.</summary>
    private static void SetRetryClaimedAt(FailedMessage message, DateTime at) =>
        typeof(FailedMessage).GetProperty(nameof(FailedMessage.RetryClaimedAt))!.SetValue(message, at);

    [Fact]
    public async Task Discard_RetryRequestedRow_WithRecentClaim_IsSkippedAsPublishing()
    {
        var faultedAt = new DateTime(2026, 1, 1, 9, 0, 0);
        var claimedAt = faultedAt.AddMinutes(1);
        var now = claimedAt + FailedMessageRetryClaimPolicy.StaleAfter - TimeSpan.FromSeconds(1);

        await using var db = CreateInMemoryDbContext();
        var message = SeedPending(db, faultedAt);
        message.RequestRetry("someone-else", null, faultedAt);
        SetRetryClaimedAt(message, claimedAt);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new DiscardFailedMessagesCommandHandler(db, CurrentUser(), FixedClock(now));
        var result = await handler.Handle(
            new DiscardFailedMessagesCommand([message.Id], "duplicate, safe to drop", null),
            CancellationToken.None);

        result.Accepted.Should().BeEmpty();
        result.Skipped.Should().ContainSingle(s => s.Id == message.Id && s.Reason == "Publishing");
        var reloaded = await db.FailedMessages.AsNoTracking().SingleAsync(m => m.Id == message.Id, TestContext.Current.CancellationToken);
        reloaded.Status.Should().Be(FailedMessageStatus.RetryRequested);
    }

    [Fact]
    public async Task Discard_RetryRequestedRow_WithStaleClaim_IsAccepted()
    {
        var faultedAt = new DateTime(2026, 1, 1, 9, 0, 0);
        var claimedAt = faultedAt.AddMinutes(1);
        var now = claimedAt + FailedMessageRetryClaimPolicy.StaleAfter + TimeSpan.FromSeconds(1);

        await using var db = CreateInMemoryDbContext();
        var message = SeedPending(db, faultedAt);
        message.RequestRetry("someone-else", null, faultedAt);
        SetRetryClaimedAt(message, claimedAt);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new DiscardFailedMessagesCommandHandler(db, CurrentUser(), FixedClock(now));
        var result = await handler.Handle(
            new DiscardFailedMessagesCommand([message.Id], "node is dead, clearing it", null),
            CancellationToken.None);

        result.Accepted.Should().ContainSingle(id => id == message.Id);
        var reloaded = await db.FailedMessages.AsNoTracking().SingleAsync(m => m.Id == message.Id, TestContext.Current.CancellationToken);
        reloaded.Status.Should().Be(FailedMessageStatus.Discarded);
    }
}
