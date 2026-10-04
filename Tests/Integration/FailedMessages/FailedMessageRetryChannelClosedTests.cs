using Integration.Domain.FailedMessages;
using Integration.FailedMessages;
using Integration.Fixtures;
using Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
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

namespace Integration.FailedMessages;

/// <summary>
/// If the retry channel dies mid-loop (the broker connection
/// drops between two candidates), publishing anyway misclassifies the result as
/// <see cref="FailedMessageCollectorService.RetryPublishOutcome.UnknownOutcome"/> and keeps the claim for
/// the full 5-minute stale window, even though nothing was ever attempted.
/// <see cref="FailedMessageCollectorService.RetryCandidatesAsync"/> must check <c>channel.IsOpen</c>
/// before each publish and, once closed, release ONLY the claim it just took for the CURRENT candidate,
/// then stop — never touching any later candidate's claim, since this instance never took it and another
/// collector instance may have legitimately claimed it in the meantime (clearing that would cause a
/// double publish). Drives it directly against a REAL SQL Server (testcontainer, via
/// <see cref="IntegrationTestFixture"/>) — the claim/clear UPDATEs are <c>ExecuteUpdateAsync</c>,
/// unsupported by the InMemory provider used in the unit-test suite — with a substitute
/// <see cref="IChannel"/> standing in for the broker connection, since only its <c>IsOpen</c> state
/// matters here, not real message delivery.
/// </summary>
[Collection("Integration")]
public class FailedMessageRetryChannelClosedTests(IntegrationTestFixture fixture)
{
    /// <summary>
    /// RetryCandidatesAsync opens a FRESH scope/DbContext per candidate via the real
    /// IServiceScopeFactory below, so its ExecuteUpdateAsync claim/clear calls hit the real SQL Server
    /// testcontainer — a bare NSubstitute&lt;IServiceScopeFactory&gt; (unconfigured) would return null
    /// from CreateScope() and NullReferenceException. Only the channel is substituted.
    /// </summary>
    private FailedMessageCollectorService CreateCollector(TimeSpan? retryPublishTimeout = null)
    {
        var sp = fixture.IntegrationTestWebApplicationFactory.Services;
        return new FailedMessageCollectorService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<ILogger<FailedMessageCollectorService>>(),
            sp.GetRequiredService<IDateTimeProvider>(),
            Options.Create(new FailedMessagesOptions()),
            sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<ReceiveEndpointDiscoveryObserver>())
        {
            RetryPublishTimeout = retryPublishTimeout ?? TimeSpan.FromSeconds(30)
        };
    }

    private async Task<FailedMessage> SeedRetryRequestedAsync(IntegrationDbContext db, DateTime faultedAt)
    {
        var message = FailedMessage.Create(
            Environment.MachineName, "appraisal-sync", FailedMessageKind.Error, Guid.CreateVersion7(), null,
            "Test.FakeEvent", "Test.FakeConsumer", "System.TimeoutException", "boom", null, 0,
            faultedAt, faultedAt, null, null, null, "{}"u8.ToArray(), "application/json", null);
        message.RequestRetry("test-actor", null, faultedAt);

        db.FailedMessages.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return message;
    }

    /// <summary>Sets the private-setter RetryClaimedAt directly — the smallest way to simulate a row
    /// ANOTHER collector instance legitimately claimed a moment ago, without a second real collector.</summary>
    private static void SetRetryClaimedAt(FailedMessage message, DateTime at) =>
        typeof(FailedMessage).GetProperty(nameof(FailedMessage.RetryClaimedAt))!.SetValue(message, at);

    [Fact]
    public async Task RetryCandidatesAsync_ChannelClosesBeforeSecondCandidate_ReleasesOnlyItsOwnClaim_LeavesLaterCandidatesUntouched()
    {
        using var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;

        var first = await SeedRetryRequestedAsync(db, now);
        var second = await SeedRetryRequestedAsync(db, now.AddSeconds(1));
        // "third" simulates a row a DIFFERENT collector instance already legitimately claimed — its
        // RetryClaimedAt must survive this instance's abort completely untouched.
        var third = await SeedRetryRequestedAsync(db, now.AddSeconds(2));
        var anotherInstancesClaim = now.AddSeconds(-1);
        SetRetryClaimedAt(third, anotherInstancesClaim);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var candidateIds = new List<Guid> { first.Id, second.Id, third.Id };

        // Open for the first candidate's publish, then closed from the second candidate onward — a
        // substitute stands in for "the broker connection dropped mid-loop" without needing a real
        // RabbitMQ outage.
        var channel = Substitute.For<IChannel>();
        channel.IsOpen.Returns(true, false, false);
        channel.BasicPublishAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        var collector = CreateCollector();
        await collector.RetryCandidatesAsync(channel, candidateIds, TestContext.Current.CancellationToken);

        // Only the first candidate was ever published to — the channel-closed check stopped the loop
        // before the second candidate's publish, so it never even reached PublishRetryAsync.
        await channel.Received(1).BasicPublishAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<BasicProperties>(),
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());

        var rows = await db.FailedMessages.AsNoTracking()
            .Where(m => candidateIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, TestContext.Current.CancellationToken);

        // The first candidate published successfully — confirmed publish, so SaveMarkRetriedWithRetryAsync
        // moved it on to Retried with the claim cleared as part of that normal path.
        Assert.Equal(FailedMessageStatus.Retried, rows[first.Id].Status);
        Assert.Null(rows[first.Id].RetryClaimedAt);

        // The second candidate is THIS instance's own claim — the channel-closed branch must release it
        // (not leave it RetryClaimedAt-stamped for the full 5-minute stale window) rather than treating
        // the abort as an unknown outcome.
        Assert.Equal(FailedMessageStatus.RetryRequested, rows[second.Id].Status);
        Assert.Null(rows[second.Id].RetryClaimedAt);

        // The third candidate was NEVER reached by this loop at all (it breaks on the second) — its
        // pre-existing claim, simulating another instance's in-flight retry, must be completely untouched.
        Assert.Equal(FailedMessageStatus.RetryRequested, rows[third.Id].Status);
        Assert.Equal(anotherInstancesClaim, rows[third.Id].RetryClaimedAt);
    }

    /// <summary>
    /// A broker that never confirms (memory/disk alarm) would otherwise cost RetryPublishTimeout PER candidate
    /// (200 x 30s = 100 minutes with Collect and Snapshot blocked behind it). The first timed-out publish keeps
    /// its claim (outcome unknown) and STOPS the loop; the remaining candidates are never claimed, so the next
    /// round starts on them.
    /// </summary>
    [Fact]
    public async Task RetryCandidatesAsync_BrokerNeverConfirms_KeepsFirstClaim_StopsLoop_LeavesRestUnclaimed()
    {
        using var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;

        var first = await SeedRetryRequestedAsync(db, now);
        var second = await SeedRetryRequestedAsync(db, now.AddSeconds(1));
        var third = await SeedRetryRequestedAsync(db, now.AddSeconds(2));
        var candidateIds = new List<Guid> { first.Id, second.Id, third.Id };

        var channel = Substitute.For<IChannel>();
        channel.IsOpen.Returns(true);
        channel.BasicPublishAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new ValueTask(Task.Delay(Timeout.Infinite, callInfo.ArgAt<CancellationToken>(5))));

        var collector = CreateCollector(retryPublishTimeout: TimeSpan.FromMilliseconds(100));
        await collector.RetryCandidatesAsync(channel, candidateIds, TestContext.Current.CancellationToken);

        await channel.Received(1).BasicPublishAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<BasicProperties>(),
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());

        var rows = await db.FailedMessages.AsNoTracking()
            .Where(m => candidateIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, TestContext.Current.CancellationToken);

        Assert.Equal(FailedMessageStatus.RetryRequested, rows[first.Id].Status);
        Assert.NotNull(rows[first.Id].RetryClaimedAt);
        Assert.Equal(FailedMessageStatus.RetryRequested, rows[second.Id].Status);
        Assert.Null(rows[second.Id].RetryClaimedAt);
        Assert.Equal(FailedMessageStatus.RetryRequested, rows[third.Id].Status);
        Assert.Null(rows[third.Id].RetryClaimedAt);
    }

    /// <summary>
    /// An explicit broker NACK (e.g. x-overflow=reject-publish on a full queue) used to just clear the claim,
    /// so the row stayed RetryRequested and was republished every round forever with no audit. It must revert
    /// to Pending with a server-generated RetryFailed audit entry — the same way an unroutable retry does — so
    /// an operator sees it and decides; the loop then carries on with the next candidate.
    /// </summary>
    [Fact]
    public async Task RetryCandidatesAsync_BrokerNacks_RevertsToPending_WritesRetryFailedAudit_ContinuesLoop()
    {
        using var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;

        var nacked = await SeedRetryRequestedAsync(db, now);
        var confirmed = await SeedRetryRequestedAsync(db, now.AddSeconds(1));
        var candidateIds = new List<Guid> { nacked.Id, confirmed.Id };

        var channel = Substitute.For<IChannel>();
        channel.IsOpen.Returns(true);
        channel.BasicPublishAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => throw new PublishException(publishSequenceNumber: 1, isReturn: false),
                _ => ValueTask.CompletedTask);

        var collector = CreateCollector();
        await collector.RetryCandidatesAsync(channel, candidateIds, TestContext.Current.CancellationToken);

        var rows = await db.FailedMessages.AsNoTracking()
            .Where(m => candidateIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, TestContext.Current.CancellationToken);

        Assert.Equal(FailedMessageStatus.Pending, rows[nacked.Id].Status);
        Assert.Null(rows[nacked.Id].RetryClaimedAt);
        Assert.Contains("nack", rows[nacked.Id].ActionReason, StringComparison.OrdinalIgnoreCase);

        var audit = await db.FailedMessageAuditLogs.AsNoTracking().SingleOrDefaultAsync(
            a => a.TargetId == nacked.Id && a.Action == FailedMessageAuditAction.RetryFailed,
            TestContext.Current.CancellationToken);
        Assert.NotNull(audit);
        Assert.Equal(FailedMessageAuditSource.Consumer, audit!.Source);
        Assert.Null(audit.ActorCode);

        Assert.Equal(FailedMessageStatus.Retried, rows[confirmed.Id].Status);
    }

    /// <summary>
    /// A publish the broker answers by closing the CHANNEL (soft error, e.g. 406) throws
    /// OperationInterruptedException — a deterministic rejection of this row, so it must revert to Pending with a
    /// RetryFailed audit (not keep its claim and be republished every 5 minutes forever). The channel is dead,
    /// so the loop stops and later candidates are never claimed.
    /// </summary>
    [Fact]
    public async Task RetryCandidatesAsync_BrokerClosesChannelOnPublish_RevertsToPending_WritesRetryFailedAudit_StopsLoop()
    {
        using var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;

        var rejected = await SeedRetryRequestedAsync(db, now);
        var untouched = await SeedRetryRequestedAsync(db, now.AddSeconds(1));
        var candidateIds = new List<Guid> { rejected.Id, untouched.Id };

        var channel = Substitute.For<IChannel>();
        channel.IsOpen.Returns(true);
        channel.BasicPublishAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new OperationInterruptedException(new ShutdownEventArgs(
                ShutdownInitiator.Peer, 406, "PRECONDITION_FAILED", cause: null!, CancellationToken.None)));

        var collector = CreateCollector();
        await collector.RetryCandidatesAsync(channel, candidateIds, TestContext.Current.CancellationToken);

        await channel.Received(1).BasicPublishAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<BasicProperties>(),
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());

        var rows = await db.FailedMessages.AsNoTracking()
            .Where(m => candidateIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, TestContext.Current.CancellationToken);

        Assert.Equal(FailedMessageStatus.Pending, rows[rejected.Id].Status);
        Assert.Null(rows[rejected.Id].RetryClaimedAt);
        Assert.Contains("channel", rows[rejected.Id].ActionReason, StringComparison.OrdinalIgnoreCase);

        var audit = await db.FailedMessageAuditLogs.AsNoTracking().SingleOrDefaultAsync(
            a => a.TargetId == rejected.Id && a.Action == FailedMessageAuditAction.RetryFailed,
            TestContext.Current.CancellationToken);
        Assert.NotNull(audit);
        Assert.Equal(FailedMessageAuditSource.Consumer, audit!.Source);
        Assert.Null(audit.ActorCode);

        // The loop stopped: the next candidate was never claimed.
        Assert.Equal(FailedMessageStatus.RetryRequested, rows[untouched.Id].Status);
        Assert.Null(rows[untouched.Id].RetryClaimedAt);
    }
}
