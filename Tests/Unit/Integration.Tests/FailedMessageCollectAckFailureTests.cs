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
using RabbitMQ.Client;
using Shared.Messaging.Services;
using Shared.Time;

namespace Integration.Tests;

/// <summary>
/// Once SaveChanges has committed a row, an ack failure (dead/closing channel)
/// must NOT fall into the Unparseable/store-again path — that would duplicate the row. Drives
/// <see cref="FailedMessageCollectorService.ProcessOneMessageAsync"/> directly — the seam extracted for
/// exactly this — with a substitute <see cref="IChannel"/> whose BasicAckAsync always throws, and a real
/// (InMemory) <see cref="IntegrationDbContext"/> so the row count is real, not mocked.
/// </summary>
public class FailedMessageCollectAckFailureTests
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

    private static BasicGetResult BuildParseableFaultResult()
    {
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            Headers = new Dictionary<string, object?>
            {
                ["MT-Fault-ExceptionType"] = "System.TimeoutException",
                ["MT-Fault-Message"] = "boom",
            }
        };
        return new BasicGetResult(1, false, "", "appraisal-sync_error", 0, properties, "{}"u8.ToArray());
    }

    [Fact]
    public async Task ProcessOneMessageAsync_StoreSucceeds_AckThrows_DoesNotStoreASecondRow()
    {
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(new DateTime(2026, 1, 1, 9, 0, 0));
        var collector = CreateCollector(dateTimeProvider);

        await using var db = CreateInMemoryDbContext();
        var channel = Substitute.For<IChannel>();
        channel.BasicAckAsync(Arg.Any<ulong>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask(Task.FromException(new InvalidOperationException("channel closed"))));

        var result = BuildParseableFaultResult();

        var keepGoing = await collector.ProcessOneMessageAsync(
            db, channel, "appraisal-sync", FailedMessageKind.Error, "appraisal-sync_error", result,
            CancellationToken.None);

        keepGoing.Should().BeFalse("an ack failure stops this queue's loop — the message will simply be redelivered");

        var count = await db.FailedMessages.CountAsync(TestContext.Current.CancellationToken);
        count.Should().Be(1, "the row was already stored before the ack failed — it must not be stored again");

        // Never nacked either — the message is genuinely stored; nacking a message whose ack merely
        // failed to reach the broker would republish it while a copy already committed.
        await channel.DidNotReceive().BasicNackAsync(
            Arg.Any<ulong>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessOneMessageAsync_StoreSucceeds_AckSucceeds_KeepsGoing()
    {
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(new DateTime(2026, 1, 1, 9, 0, 0));
        var collector = CreateCollector(dateTimeProvider);

        await using var db = CreateInMemoryDbContext();
        var channel = Substitute.For<IChannel>();
        channel.BasicAckAsync(Arg.Any<ulong>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        var result = BuildParseableFaultResult();

        var keepGoing = await collector.ProcessOneMessageAsync(
            db, channel, "appraisal-sync", FailedMessageKind.Error, "appraisal-sync_error", result,
            CancellationToken.None);

        keepGoing.Should().BeTrue();
        (await db.FailedMessages.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }
}
