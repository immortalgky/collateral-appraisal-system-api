using FluentAssertions;
using Integration.Domain.FailedMessages;
using Integration.FailedMessages;
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

namespace Integration.Tests;

/// <summary>
/// With publisher confirms, an exception out of <c>BasicPublishAsync</c> (or the
/// confirm wait) can have an UNKNOWN outcome — the broker may or may not have the message. The retry
/// claim must be cleared ONLY when the outcome is KNOWN to be undelivered (an explicit broker nack, an
/// unroutable-queue return, or a failure before the publish call ever ran) — never for an unknown
/// outcome. Drives <see cref="FailedMessageCollectorService.PublishRetryAsync"/> directly with a
/// substitute <see cref="IChannel"/> — no real broker needed to prove the classification.
/// </summary>
public class FailedMessageRetryPublishOutcomeTests
{
    private static FailedMessageCollectorService CreateCollector(
        ILogger<FailedMessageCollectorService>? logger = null, TimeSpan? publishTimeout = null)
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
            logger ?? Substitute.For<ILogger<FailedMessageCollectorService>>(),
            Substitute.For<IDateTimeProvider>(),
            Options.Create(new FailedMessagesOptions()),
            configuration,
            new ReceiveEndpointDiscoveryObserver())
        {
            RetryPublishTimeout = publishTimeout ?? TimeSpan.FromSeconds(30)
        };
    }

    private sealed class CapturingLogger : ILogger<FailedMessageCollectorService>
    {
        public List<LogLevel> Levels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Levels.Add(logLevel);
    }

    /// <summary>A broker that never confirms (e.g. a memory/disk alarm): only cancellation ends the await.</summary>
    private static void NeverConfirms(IChannel channel) =>
        channel
            .BasicPublishAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new ValueTask(Task.Delay(Timeout.Infinite, callInfo.ArgAt<CancellationToken>(5))));

    private static FailedMessage ValidMessage() =>
        FailedMessage.Create(
            "APP-NODE-01", "appraisal-sync", FailedMessageKind.Error, Guid.NewGuid(), null, "Test.FakeEvent",
            null, "System.TimeoutException", "boom", null, 0, DateTime.Now, DateTime.Now, null, null, null,
            "{}"u8.ToArray(), "application/json", null);

    private static void ThrowsOnPublish(IChannel channel, Exception exception) =>
        channel
            .When(c => c.BasicPublishAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw exception);

    private static async Task<BasicProperties> PublishedPropertiesAsync(FailedMessage message)
    {
        BasicProperties? published = null;
        var channel = Substitute.For<IChannel>();
        await channel.BasicPublishAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Do<BasicProperties>(p => published = p),
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());

        var outcome = await CreateCollector().PublishRetryAsync(channel, message, CancellationToken.None);

        outcome.Should().Be(FailedMessageCollectorService.RetryPublishOutcome.Confirmed);
        return published!;
    }

    [Fact]
    public async Task PublishRetryAsync_UnparseableRow_DoesNotStampTheSyntheticHashAsMessageId()
    {
        // The row's MessageId is a SHA-256 content hash the original never had: stamping it would make
        // consumers/InboxGuard dedup on an id that does not exist on the wire, and give two distinct
        // messages with identical bodies one inbox key.
        var body = "not an envelope"u8.ToArray();
        var message = FailedMessage.Create(
            "APP-NODE-01", "appraisal-sync", FailedMessageKind.Error,
            FailedMessageCollectorService.DeterministicMessageId("appraisal-sync", FailedMessageKind.Error, body),
            null, "Unknown", null, FailedMessageCollectorService.UnparseableExceptionType, "bad", null, 0,
            DateTime.Now, DateTime.Now, null, null, null, body, "application/json", null);

        var properties = await PublishedPropertiesAsync(message);

        properties.MessageId.Should().BeNull();
    }

    [Fact]
    public async Task PublishRetryAsync_ParseableRow_KeepsItsRealMessageId()
    {
        var message = ValidMessage();

        var properties = await PublishedPropertiesAsync(message);

        properties.MessageId.Should().Be(message.MessageId!.Value.ToString());
    }

    [Fact]
    public async Task PublishRetryAsync_StripsBrokerOwnedDeadLetterHeaders_KeepsOrdinaryOnes()
    {
        // CoerceHeaders flattened every stored header to a string. A string-typed x-death (RabbitMQ expects an
        // array of tables) can break the broker's handling on a later dead-letter; the broker re-creates these.
        var message = FailedMessage.Create(
            "APP-NODE-01", "appraisal-sync", FailedMessageKind.Error, Guid.NewGuid(), null, "Test.FakeEvent",
            null, "System.TimeoutException", "boom", null, 0, DateTime.Now, DateTime.Now, null, null, null,
            "{}"u8.ToArray(), "application/json",
            new Dictionary<string, string>
            {
                ["x-death"] = "[]",
                ["x-first-death-queue"] = "appraisal-sync",
                ["x-first-death-reason"] = "rejected",
                ["x-last-death-exchange"] = "",
                ["x-delivery-count"] = "3",
                ["MT-Fault-Message"] = "boom",
                ["my-header"] = "keep-me",
            });

        var properties = await PublishedPropertiesAsync(message);

        properties.Headers.Should().NotBeNull();
        properties.Headers!.Keys.Should().BeEquivalentTo(["my-header"]);
        properties.Headers["my-header"].Should().Be("keep-me");
    }

    [Fact]
    public async Task PublishRetryAsync_PublishThrowsUnknownException_ReturnsUnknownOutcome()
    {
        var channel = Substitute.For<IChannel>();
        ThrowsOnPublish(channel, new TimeoutException("confirm wait timed out"));

        var collector = CreateCollector();
        var outcome = await collector.PublishRetryAsync(channel, ValidMessage(), CancellationToken.None);

        outcome.Should().Be(FailedMessageCollectorService.RetryPublishOutcome.UnknownOutcome,
            "the broker may or may not have the message — the claim must be KEPT, not cleared");
    }

    [Fact]
    public async Task PublishRetryAsync_ExplicitBrokerNack_ReturnsKnownUndeliveredNacked()
    {
        var channel = Substitute.For<IChannel>();
        ThrowsOnPublish(channel, new PublishException(publishSequenceNumber: 1, isReturn: false));

        var collector = CreateCollector();
        var outcome = await collector.PublishRetryAsync(channel, ValidMessage(), CancellationToken.None);

        outcome.Should().Be(FailedMessageCollectorService.RetryPublishOutcome.KnownUndeliveredNacked,
            "IsReturn == false is RabbitMQ.Client's own explicit-nack signal — KNOWN undelivered");
    }

    [Fact]
    public async Task PublishRetryAsync_UnroutablePublishReturn_ReturnsUnroutableQueueGone()
    {
        var channel = Substitute.For<IChannel>();
        ThrowsOnPublish(channel, new PublishException(publishSequenceNumber: 1, isReturn: true));

        var collector = CreateCollector();
        var outcome = await collector.PublishRetryAsync(channel, ValidMessage(), CancellationToken.None);

        outcome.Should().Be(FailedMessageCollectorService.RetryPublishOutcome.UnroutableQueueGone);
    }

    private static OperationInterruptedException ChannelClosedByBroker(ushort replyCode, string replyText) =>
        new(new ShutdownEventArgs(ShutdownInitiator.Peer, replyCode, replyText, cause: null!, CancellationToken.None));

    /// <summary>A mandatory publish the broker answers by closing the CHANNEL (e.g. 406 precondition-failed,
    /// 403 access-refused) throws OperationInterruptedException, not PublishException. The broker deterministically
    /// refused this publish, so it is KNOWN undelivered: reverted with a RetryFailed audit rather than kept as an
    /// unknown outcome and republished every 5 minutes forever.</summary>
    [Theory]
    [InlineData((ushort)403, "ACCESS_REFUSED")]
    [InlineData((ushort)404, "NOT_FOUND")]
    [InlineData((ushort)405, "RESOURCE_LOCKED")]
    [InlineData((ushort)406, "PRECONDITION_FAILED")]
    public async Task PublishRetryAsync_BrokerClosesChannelWithSoftError_ReturnsKnownUndeliveredChannelClosed(
        ushort replyCode, string replyText)
    {
        var channel = Substitute.For<IChannel>();
        ThrowsOnPublish(channel, ChannelClosedByBroker(replyCode, replyText));

        var outcome = await CreateCollector().PublishRetryAsync(channel, ValidMessage(), CancellationToken.None);

        outcome.Should().Be(FailedMessageCollectorService.RetryPublishOutcome.KnownUndeliveredChannelClosed);
    }

    /// <summary>A connection-level close (320 connection-forced, 541 internal-error, ...) says nothing about whether the
    /// broker took this message: it stays an UNKNOWN outcome with the claim kept.</summary>
    [Theory]
    [InlineData((ushort)320, "CONNECTION_FORCED")]
    [InlineData((ushort)541, "INTERNAL_ERROR")]
    public async Task PublishRetryAsync_BrokerClosesConnection_StaysUnknownOutcome(ushort replyCode, string replyText)
    {
        var channel = Substitute.For<IChannel>();
        ThrowsOnPublish(channel, ChannelClosedByBroker(replyCode, replyText));

        var outcome = await CreateCollector().PublishRetryAsync(channel, ValidMessage(), CancellationToken.None);

        outcome.Should().Be(FailedMessageCollectorService.RetryPublishOutcome.UnknownOutcome);
    }

    /// <summary>A failure BEFORE the publish call ever runs (building headers/properties) — nothing
    /// reached the broker, so it's safe to clear the claim.</summary>
    [Fact]
    public async Task PublishRetryAsync_FailsBeforePublish_ReturnsFailedBeforePublish_NeverCallsChannel()
    {
        var message = ValidMessage();
        // BuildRetryHeaders enumerates Headers — forcing it to null (bypassing the public API, which
        // never allows this) is the smallest way to trigger a failure genuinely BEFORE the publish call.
        typeof(FailedMessage).GetProperty(nameof(FailedMessage.Headers))!.SetValue(message, null);

        var channel = Substitute.For<IChannel>();
        var collector = CreateCollector();

        var outcome = await collector.PublishRetryAsync(channel, message, CancellationToken.None);

        outcome.Should().Be(FailedMessageCollectorService.RetryPublishOutcome.FailedBeforePublish);
        await channel.DidNotReceive().BasicPublishAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<BasicProperties>(),
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
    }

    /// <summary>A broker that never confirms must not stall the collector round forever — the publish is
    /// bounded, and a timeout is an UNKNOWN outcome (the broker may still have taken it): claim kept — reported
    /// as its own outcome so the retry loop can stop instead of burning a timeout per remaining candidate.</summary>
    [Fact]
    public async Task PublishRetryAsync_BrokerNeverConfirms_TimesOutAsUnknownOutcomeTimedOut()
    {
        var channel = Substitute.For<IChannel>();
        NeverConfirms(channel);

        var collector = CreateCollector(publishTimeout: TimeSpan.FromMilliseconds(50));
        var outcome = await collector.PublishRetryAsync(channel, ValidMessage(), CancellationToken.None);

        outcome.Should().Be(FailedMessageCollectorService.RetryPublishOutcome.UnknownOutcomeTimedOut);
    }

    /// <summary>Host shutdown is not an "outcome UNKNOWN" warning: it propagates and is logged at Information.</summary>
    [Fact]
    public async Task PublishRetryAsync_HostShutdown_PropagatesCancellation_NoWarning()
    {
        var channel = Substitute.For<IChannel>();
        NeverConfirms(channel);
        var logger = new CapturingLogger();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var collector = CreateCollector(logger);
        var act = async () => await collector.PublishRetryAsync(channel, ValidMessage(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        logger.Levels.Should().Contain(LogLevel.Information).And.NotContain(LogLevel.Warning);
    }
}
