using System.Text.Json;
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
using Shared.Messaging.Services;
using Shared.Time;

namespace Integration.FailedMessages;

/// <summary>
/// An Error row's dedup key includes FaultedAt = MT-Fault-Timestamp, which is unique per fault. So when the
/// unique index (real SQL Server, not InMemory) rejects an insert, it can only be a redelivery of the SAME
/// broker message (e.g. its ack was lost) — even if the stored row was already Retried or Discarded. The
/// collector must ack and skip, not insert a phantom Pending duplicate that could be retried (and
/// republished) a second time. A genuine re-failure after a retry carries a new MT-Fault-Timestamp, so it
/// has a new key and is inserted.
/// </summary>
[Collection("Integration")]
public class FailedMessageErrorDedupTests(IntegrationTestFixture fixture)
{
    private IServiceProvider Services => fixture.IntegrationTestWebApplicationFactory.Services;

    private FailedMessageCollectorService CreateCollector() =>
        new(
            Services.GetRequiredService<IServiceScopeFactory>(),
            Services.GetRequiredService<IHttpClientFactory>(),
            Services.GetRequiredService<ILogger<FailedMessageCollectorService>>(),
            Services.GetRequiredService<IDateTimeProvider>(),
            Options.Create(new FailedMessagesOptions()),
            Services.GetRequiredService<IConfiguration>(),
            Services.GetRequiredService<ReceiveEndpointDiscoveryObserver>());

    private static BasicGetResult BuildErrorResult(Guid messageId, string faultTimestampUtc)
    {
        var envelope = JsonSerializer.Serialize(new
        {
            messageId,
            messageType = new[] { "urn:message:Test:FakeEvent" },
            sentTime = "2026-01-01T08:59:00Z",
            message = new { }
        });

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            Headers = new Dictionary<string, object?>
            {
                ["MT-Fault-ExceptionType"] = "System.TimeoutException",
                ["MT-Fault-Message"] = "boom",
                ["MT-Fault-Timestamp"] = faultTimestampUtc
            }
        };

        return new BasicGetResult(1, false, "", "fm-dedup_error", 0, properties,
            System.Text.Encoding.UTF8.GetBytes(envelope));
    }

    private static IChannel AckingChannel()
    {
        var channel = Substitute.For<IChannel>();
        channel.BasicAckAsync(Arg.Any<ulong>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);
        return channel;
    }

    /// <summary>Collects the message once, then moves the stored row to Retried the way a real retry would.</summary>
    private async Task<(FailedMessageCollectorService Collector, IChannel Channel, string Queue)> CollectAndRetryAsync(
        Guid messageId, string sourceQueue)
    {
        var collector = CreateCollector();
        var channel = AckingChannel();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();

        var keepGoing = await collector.ProcessOneMessageAsync(
            db, channel, sourceQueue, FailedMessageKind.Error, sourceQueue + "_error",
            BuildErrorResult(messageId, "2026-01-01T09:00:00Z"), TestContext.Current.CancellationToken);
        Assert.True(keepGoing);

        var row = await db.FailedMessages.SingleAsync(
            m => m.MessageId == messageId && m.SourceQueue == sourceQueue, TestContext.Current.CancellationToken);
        var now = Services.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        Assert.True(row.RequestRetry("test-actor", null, now));
        Assert.True(row.MarkRetried(now));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (collector, channel, sourceQueue + "_error");
    }

    private async Task<List<FailedMessage>> ReadRowsAsync(Guid messageId, string sourceQueue)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        return await db.FailedMessages.AsNoTracking()
            .Where(m => m.MessageId == messageId && m.SourceQueue == sourceQueue)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Redelivery_OfSameErrorMessage_AfterRetried_IsAckedWithoutInsertingPhantomPending()
    {
        var messageId = Guid.CreateVersion7();
        var sourceQueue = $"fm-dedup-{Guid.NewGuid():N}"[..24];
        var (collector, channel, errorQueue) = await CollectAndRetryAsync(messageId, sourceQueue);

        // The broker redelivers the very same message (same MessageId, same MT-Fault-Timestamp): its ack
        // was lost after the first insert.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var keepGoing = await collector.ProcessOneMessageAsync(
            db, channel, sourceQueue, FailedMessageKind.Error, errorQueue,
            BuildErrorResult(messageId, "2026-01-01T09:00:00Z"), TestContext.Current.CancellationToken);

        Assert.True(keepGoing, "a duplicate is acked, so the queue loop keeps going");

        var rows = await ReadRowsAsync(messageId, sourceQueue);
        var only = Assert.Single(rows);
        Assert.Equal(FailedMessageStatus.Retried, only.Status);
    }

    [Fact]
    public async Task NewFault_OfSameMessage_AfterRetried_InsertsNewPendingRow()
    {
        var messageId = Guid.CreateVersion7();
        var sourceQueue = $"fm-dedup-{Guid.NewGuid():N}"[..24];
        var (collector, channel, errorQueue) = await CollectAndRetryAsync(messageId, sourceQueue);

        // The retried message failed again: same MessageId, but a NEW MT-Fault-Timestamp, so a new key.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var keepGoing = await collector.ProcessOneMessageAsync(
            db, channel, sourceQueue, FailedMessageKind.Error, errorQueue,
            BuildErrorResult(messageId, "2026-01-01T09:10:00Z"), TestContext.Current.CancellationToken);

        Assert.True(keepGoing);

        var rows = await ReadRowsAsync(messageId, sourceQueue);
        Assert.Equal(2, rows.Count);
        Assert.Single(rows, m => m.Status == FailedMessageStatus.Retried);
        Assert.Single(rows, m => m.Status == FailedMessageStatus.Pending);
    }
}
