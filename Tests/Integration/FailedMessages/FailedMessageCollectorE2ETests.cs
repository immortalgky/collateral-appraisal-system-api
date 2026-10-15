using System.Text.Json;
using Integration.Domain.FailedMessages;
using Integration.FailedMessages;
using Integration.Fixtures;
using Integration.Infrastructure;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Shared.Exceptions;
using Shared.Messaging.Services;
using Shared.Time;

namespace Integration.FailedMessages;

/// <summary>
/// Collector end-to-end (design §3): a REAL MassTransit fault (real <c>MT-Fault-*</c> headers, produced
/// by a throwaway bus + consumer against the RabbitMq testcontainer) is drained by
/// <see cref="FailedMessageCollectorService"/> into <see cref="FailedMessage"/>, retried back to the
/// source queue with fault headers stripped, and a retry to a queue that no longer exists is reverted to
/// <see cref="FailedMessageStatus.Pending"/> (with a <c>RetryFailed</c> audit entry) rather than silently
/// dropped or left stuck RetryRequested forever.
///
/// The collector's Discover step normally talks to the RabbitMQ Management API (not exposed by the
/// fixture's container). Feeding the exact <c>{queue}_error</c> name into the internal
/// <see cref="FailedMessageCollectorService.RunRoundAsync(CancellationToken, IReadOnlyList{string}?)"/>
/// overload is the smallest way around that — Collect/Retry/Snapshot are exercised for real either way.
/// </summary>
public record FmE2eTestMessage(Guid Id);

public class ThrowingConsumer : IConsumer<FmE2eTestMessage>
{
    public Task Consume(ConsumeContext<FmE2eTestMessage> context) => throw new ConflictException("boom");
}

[Collection("Integration")]
public class FailedMessageCollectorE2ETests(IntegrationTestFixture fixture)
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(20);
    private readonly string _node = Environment.MachineName;

    private string RabbitConnectionString => fixture.RabbitMq.GetConnectionString();

    private FailedMessageCollectorService CreateCollector()
    {
        var sp = fixture.IntegrationTestWebApplicationFactory.Services;
        return new FailedMessageCollectorService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<ILogger<FailedMessageCollectorService>>(),
            sp.GetRequiredService<IDateTimeProvider>(),
            Options.Create(new FailedMessagesOptions()),
            sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<ReceiveEndpointDiscoveryObserver>());
    }

    private async Task<IConnection> OpenRawConnectionAsync()
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(RabbitConnectionString),
            UserName = "testuser",
            Password = "testpw"
        };
        return await factory.CreateConnectionAsync(TestContext.Current.CancellationToken);
    }

    private static async Task WaitForMessageAsync(IConnection connection, string queue, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await using var channel = await connection.CreateChannelAsync(
                    new CreateChannelOptions(publisherConfirmationsEnabled: false, publisherConfirmationTrackingEnabled: false),
                    TestContext.Current.CancellationToken);
                var ok = await channel.QueueDeclarePassiveAsync(queue, TestContext.Current.CancellationToken);
                if (ok.MessageCount > 0)
                    return;
            }
            catch
            {
                // Queue doesn't exist yet — MassTransit hasn't created the fault twin, keep polling.
            }

            await Task.Delay(200, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"Queue '{queue}' never received a message within {timeout}.");
    }

    [Fact]
    public async Task Collector_DrainsRealMassTransitFault_ThenRetriesBackToSourceQueueWithFaultHeadersStripped()
    {
        var queueName = $"fm-e2e-test-{Guid.NewGuid():N}"[..24];
        var errorQueue = queueName + "_error";

        var bus = Bus.Factory.CreateUsingRabbitMq(cfg =>
        {
            cfg.Host(new Uri(RabbitConnectionString), h =>
            {
                h.Username("testuser");
                h.Password("testpw");
            });

            cfg.ReceiveEndpoint(queueName, e => e.Consumer(() => new ThrowingConsumer()));
        });

        await bus.StartAsync(TestContext.Current.CancellationToken);
        await using var rawConnection = await OpenRawConnectionAsync();

        try
        {
            await bus.Publish(new FmE2eTestMessage(Guid.NewGuid()), TestContext.Current.CancellationToken);
            await WaitForMessageAsync(rawConnection, errorQueue, WaitTimeout);
        }
        finally
        {
            // Stop the throwaway bus's own connection before the collector drains the queue — leaves
            // only the collector's connection and this test's raw inspection connection.
            await bus.StopAsync(TestContext.Current.CancellationToken);
        }

        var collector = CreateCollector();
        await collector.RunRoundAsync(TestContext.Current.CancellationToken, [errorQueue]);

        using var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var collected = await db.FailedMessages.AsNoTracking()
            .SingleAsync(m => m.Node == _node && m.SourceQueue == queueName, TestContext.Current.CancellationToken);

        Assert.Equal(FailedMessageKind.Error, collected.Kind);
        Assert.Equal(FailedMessageStatus.Pending, collected.Status);
        // MassTransit's real MT-Fault-ExceptionType value for a thrown Shared.Exceptions.ConflictException.
        Assert.Equal("Shared.Exceptions.ConflictException", collected.ExceptionType);
        Assert.True(ExceptionTypeClassifier.IsNonTransient(collected.ExceptionType),
            $"Expected '{collected.ExceptionType}' to classify as non-transient (design: simple name match).");

        // Mark RetryRequested and run another round — Retry doesn't need the Discover list.
        var tracked = await db.FailedMessages.SingleAsync(m => m.Id == collected.Id, TestContext.Current.CancellationToken);
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        Assert.True(tracked.RequestRetry("test-actor", "e2e retry", dateTimeProvider.ApplicationNow));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await collector.RunRoundAsync(TestContext.Current.CancellationToken, []);

        using var verifyScope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var afterRetry = await verifyDb.FailedMessages.AsNoTracking()
            .SingleAsync(m => m.Id == collected.Id, TestContext.Current.CancellationToken);
        Assert.Equal(FailedMessageStatus.Retried, afterRetry.Status);

        await using var inspectChannel = await rawConnection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: false, publisherConfirmationTrackingEnabled: false),
            TestContext.Current.CancellationToken);
        var republished = await inspectChannel.BasicGetAsync(queueName, autoAck: true, TestContext.Current.CancellationToken);
        Assert.NotNull(republished);
        var headerKeys = republished!.BasicProperties.Headers?.Keys.Select(k => k.ToString()).ToList() ?? [];
        Assert.DoesNotContain(headerKeys, k => k!.StartsWith("MT-Fault-", StringComparison.Ordinal));
        Assert.DoesNotContain("MT-Reason", headerKeys);
        // Retry republish must survive a broker restart, not just live in memory.
        Assert.True(republished.BasicProperties.Persistent);
        Assert.Equal(collected.MessageId.ToString(), republished.BasicProperties.MessageId);

        // Best-effort cleanup — a throwaway per-test queue, but no reason to leave it on the broker.
        try
        {
            await inspectChannel.QueueDeleteAsync(queueName, cancellationToken: TestContext.Current.CancellationToken);
            await inspectChannel.QueueDeleteAsync(errorQueue, cancellationToken: TestContext.Current.CancellationToken);
        }
        catch
        {
            // Not load-bearing for the assertions above.
        }
    }

    /// <summary>
    /// Two collector instances sharing the SAME node name (an IIS overlapped recycle, or a second
    /// process) must never both publish the same RetryRequested row — the claim's single conditional
    /// UPDATE lets exactly one of them win. Real RabbitMQ: draining the target queue after both instances
    /// race must yield exactly one message, not two.
    /// </summary>
    [Fact]
    public async Task Collector_TwoInstancesSameNode_ConcurrentRetry_PublishesExactlyOnce()
    {
        using var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var now = dateTimeProvider.ApplicationNow;

        var baseQueue = $"fm-claim-test-{Guid.NewGuid():N}"[..24];

        await using var rawConnection = await OpenRawConnectionAsync();
        await using var channel = await rawConnection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: false, publisherConfirmationTrackingEnabled: false),
            TestContext.Current.CancellationToken);
        await channel.QueueDeclareAsync(baseQueue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: TestContext.Current.CancellationToken);

        try
        {
            var message = FailedMessage.Create(
                _node, baseQueue, FailedMessageKind.Error, Guid.CreateVersion7(), null, "Test.FakeEvent",
                null, "System.TimeoutException", "boom", null, 0, now, now, null, null, null, "{}"u8.ToArray(),
                "application/json", null);
            Assert.True(message.RequestRetry("test-actor", null, now));
            db.FailedMessages.Add(message);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Two independent collector instances — same node (Environment.MachineName), same DB.
            var collectorA = CreateCollector();
            var collectorB = CreateCollector();

            await Task.WhenAll(
                collectorA.RunRoundAsync(TestContext.Current.CancellationToken, []),
                collectorB.RunRoundAsync(TestContext.Current.CancellationToken, []));

            using var verifyScope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
            var reloaded = await verifyDb.FailedMessages.AsNoTracking()
                .SingleAsync(m => m.Id == message.Id, TestContext.Current.CancellationToken);
            Assert.Equal(FailedMessageStatus.Retried, reloaded.Status);
            Assert.Null(reloaded.RetryClaimedAt);

            var first = await channel.BasicGetAsync(baseQueue, autoAck: true, TestContext.Current.CancellationToken);
            Assert.NotNull(first);
            var second = await channel.BasicGetAsync(baseQueue, autoAck: true, TestContext.Current.CancellationToken);
            Assert.Null(second);
        }
        finally
        {
            try
            {
                await channel.QueueDeleteAsync(baseQueue, cancellationToken: TestContext.Current.CancellationToken);
            }
            catch
            {
                // Not load-bearing for the assertions above.
            }
        }
    }

    /// <summary>
    /// A retry that comes back UNROUTABLE (original queue since deleted) must not be left stuck
    /// RetryRequested forever — the collector reverts it to Pending with a system-generated
    /// <c>RetryFailed</c> audit entry instead (see design.md "node decommissioning").
    /// </summary>
    [Fact]
    public async Task Collector_RetryToQueueThatNoLongerExists_RevertsToPendingWithRetryFailedAudit()
    {
        using var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var now = dateTimeProvider.ApplicationNow;

        var missingQueue = $"fm-e2e-missing-{Guid.NewGuid():N}"[..24];
        var message = FailedMessage.Create(
            _node, missingQueue, FailedMessageKind.Error, Guid.CreateVersion7(), null, "Test.FakeEvent",
            null, "System.TimeoutException", "boom", null, 0, now, now, null, null, null, "{}"u8.ToArray(),
            "application/json", null);
        Assert.True(message.RequestRetry("test-actor", null, now));
        db.FailedMessages.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var collector = CreateCollector();
        await collector.RunRoundAsync(TestContext.Current.CancellationToken, []);

        using var verifyScope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var reloaded = await verifyDb.FailedMessages.AsNoTracking()
            .SingleAsync(m => m.Id == message.Id, TestContext.Current.CancellationToken);
        Assert.Equal(FailedMessageStatus.Pending, reloaded.Status);
        Assert.Equal($"Retry failed on {_node}: queue not found", reloaded.ActionReason);

        var audit = await verifyDb.FailedMessageAuditLogs.AsNoTracking().SingleOrDefaultAsync(
            a => a.TargetId == message.Id && a.Action == "RetryFailed", TestContext.Current.CancellationToken);
        Assert.NotNull(audit);
        Assert.Equal("Consumer", audit!.Source);
        // server-generated — no signed-in user; the node is embedded in the row's own ActionReason
        // (asserted above) rather than the audit's ActorCode.
        Assert.Null(audit.ActorCode);
    }

    /// <summary>
    /// The REAL Discover step (not the `knownFaultQueues` test bypass used above) must find
    /// a fault queue for a consumer that is NOT one of the 7 hand-maintained <see cref="OrderedEndpoints"/>.
    /// The fixture's RabbitMQ container has no management plugin, so Discover's Management API call fails
    /// and falls back to <see cref="ReceiveEndpointDiscoveryObserver"/> + passive-declare — this test's
    /// throwaway bus is connected to the SAME observer instance the app registered, the smallest way to
    /// give it a queue name it didn't already know about without a real management API.
    /// </summary>
    [Fact]
    public async Task Collector_RealDiscover_FindsNonOrderedQueue_ViaObservedReceiveEndpoint()
    {
        var queueName = $"fm-e2e-fallback-{Guid.NewGuid():N}"[..24];
        var errorQueue = queueName + "_error";
        var observer = fixture.IntegrationTestWebApplicationFactory.Services
            .GetRequiredService<ReceiveEndpointDiscoveryObserver>();

        var bus = Bus.Factory.CreateUsingRabbitMq(cfg =>
        {
            cfg.Host(new Uri(RabbitConnectionString), h =>
            {
                h.Username("testuser");
                h.Password("testpw");
            });

            cfg.ReceiveEndpoint(queueName, e => e.Consumer(() => new ThrowingConsumer()));
        });

        // Same observer the app's real bus uses — connecting a second bus to it is the smallest way to
        // give it a queue name outside OrderedEndpoints without a real RabbitMQ management API.
        bus.ConnectReceiveEndpointObserver(observer);

        await bus.StartAsync(TestContext.Current.CancellationToken);
        await using var rawConnection = await OpenRawConnectionAsync();

        try
        {
            await bus.Publish(new FmE2eTestMessage(Guid.NewGuid()), TestContext.Current.CancellationToken);
            await WaitForMessageAsync(rawConnection, errorQueue, WaitTimeout);
        }
        finally
        {
            await bus.StopAsync(TestContext.Current.CancellationToken);
        }

        var collector = CreateCollector();
        // null => real Discover (Management API call, expected to fail here, then the passive-declare
        // fallback over BuildFallbackCandidates(observer.QueueNames) ∪ OrderedEndpoints).
        await collector.RunRoundAsync(TestContext.Current.CancellationToken, null);

        using var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var collected = await db.FailedMessages.AsNoTracking()
            .SingleOrDefaultAsync(m => m.Node == _node && m.SourceQueue == queueName,
                TestContext.Current.CancellationToken);

        Assert.NotNull(collected);
        Assert.Equal(FailedMessageKind.Error, collected!.Kind);

        try
        {
            await using var cleanupChannel = await rawConnection.CreateChannelAsync(
                new CreateChannelOptions(publisherConfirmationsEnabled: false, publisherConfirmationTrackingEnabled: false),
                TestContext.Current.CancellationToken);
            await cleanupChannel.QueueDeleteAsync(queueName, cancellationToken: TestContext.Current.CancellationToken);
            await cleanupChannel.QueueDeleteAsync(errorQueue, cancellationToken: TestContext.Current.CancellationToken);
        }
        catch
        {
            // Not load-bearing for the assertions above.
        }
    }

    /// <summary>MassTransit envelope shape ParseEnvelope reads (messageId/sentTime/message) — built by
    /// hand here (raw AMQP, no bus) so the SAME body bytes can be dropped into the "_skipped" queue
    /// twice with a fixed, controlled sentTime — the only way to make the second fault land on the exact
    /// same dedup key as the first without depending on MassTransit's own skip timing.</summary>
    private static byte[] BuildEnvelopeBody(Guid messageId, DateTime sentTimeUtc) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            messageId = messageId.ToString(),
            sentTime = sentTimeUtc.ToString("O"),
            message = new { id = Guid.NewGuid() }
        });

    /// <summary>
    /// A message that gets retried and then faults again (here: skipped again) keeps the same
    /// envelope, so its (MessageId, SourceQueue, Kind, FaultedAt) key collides with the ORIGINAL row —
    /// which has since moved to Retried. Before the fix this silently dropped the second failure (ack,
    /// no new row); after the fix it must insert a new Pending row instead, leaving the Retried row alone.
    /// </summary>
    [Fact]
    public async Task Collector_SkippedAgainAfterRetry_InsertsNewPendingRow_KeepsOriginalRetried()
    {
        var baseQueue = $"fm-w-a-test-{Guid.NewGuid():N}"[..24];
        var skippedQueue = baseQueue + "_skipped";
        var messageId = Guid.NewGuid();
        var body = BuildEnvelopeBody(messageId, DateTime.UtcNow);

        await using var rawConnection = await OpenRawConnectionAsync();
        await using var channel = await rawConnection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: false, publisherConfirmationTrackingEnabled: false),
            TestContext.Current.CancellationToken);

        // Retry republishes to the base queue by routing key — it must already exist, or a mandatory
        // publish to a name nothing is bound to would fault instead.
        await channel.QueueDeclareAsync(baseQueue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: TestContext.Current.CancellationToken);
        await channel.QueueDeclareAsync(skippedQueue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: TestContext.Current.CancellationToken);

        try
        {
            await channel.BasicPublishAsync(string.Empty, skippedQueue, mandatory: true, body,
                TestContext.Current.CancellationToken);

            var collector = CreateCollector();

            // Round 1: first skip is collected as Pending.
            await collector.RunRoundAsync(TestContext.Current.CancellationToken, [skippedQueue]);

            using var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
            var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
            var original = await db.FailedMessages
                .SingleAsync(m => m.MessageId == messageId && m.SourceQueue == baseQueue,
                    TestContext.Current.CancellationToken);
            Assert.Equal(FailedMessageStatus.Pending, original.Status);

            // Admin retries it — the collector republishes to the base queue and marks it Retried.
            Assert.True(original.RequestRetry("test-actor", null, dateTimeProvider.ApplicationNow));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await collector.RunRoundAsync(TestContext.Current.CancellationToken, []);

            using (var verifyScope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope())
            {
                var verifyDb = verifyScope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
                var afterRetry = await verifyDb.FailedMessages.AsNoTracking()
                    .SingleAsync(m => m.Id == original.Id, TestContext.Current.CancellationToken);
                Assert.Equal(FailedMessageStatus.Retried, afterRetry.Status);
            }

            // Move the retried message (byte-identical body — Retry never touches it) straight back into
            // "_skipped", simulating it faulting the exact same way a second time.
            var retried = await channel.BasicGetAsync(baseQueue, autoAck: true, TestContext.Current.CancellationToken);
            Assert.NotNull(retried);
            await channel.BasicPublishAsync(string.Empty, skippedQueue, mandatory: true, retried!.Body,
                TestContext.Current.CancellationToken);

            // Round 3: second skip hits the dedup key of the now-Retried `original` row.
            await collector.RunRoundAsync(TestContext.Current.CancellationToken, [skippedQueue]);

            using var finalScope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
            var finalDb = finalScope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
            var rows = await finalDb.FailedMessages.AsNoTracking()
                .Where(m => m.MessageId == messageId && m.SourceQueue == baseQueue)
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, rows.Count);
            Assert.Contains(rows, m => m.Id == original.Id && m.Status == FailedMessageStatus.Retried);
            Assert.Single(rows, m => m.Id != original.Id && m.Status == FailedMessageStatus.Pending);
        }
        finally
        {
            try
            {
                await channel.QueueDeleteAsync(baseQueue, cancellationToken: TestContext.Current.CancellationToken);
                await channel.QueueDeleteAsync(skippedQueue, cancellationToken: TestContext.Current.CancellationToken);
            }
            catch
            {
                // Not load-bearing for the assertions above.
            }
        }
    }
}
