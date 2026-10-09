using FluentAssertions;
using Integration.Domain.FailedMessages;
using Integration.FailedMessages;
using Integration.Infrastructure;
using Integration.Infrastructure.Configurations;
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
/// An Unparseable row's FaultedAt must be real time (AMQP Timestamp, else
/// collection time) — never a fixed epoch sentinel. <c>DateTime.UnixEpoch</c> sorted every Unparseable
/// row to the very bottom of the faultedAt-DESC list (effectively hidden from the screen) and made the
/// summary's <c>oldestPendingAt</c> falsely report 1970. Because FaultedAt is no longer stable across
/// redeliveries for these rows, dedup moved to an explicit existence check
/// (<see cref="FailedMessageCollectorService.AlreadyCollectedAsync"/>) on (MessageId, SourceQueue, Kind)
/// — proven here end to end against a real (InMemory) <see cref="IntegrationDbContext"/>.
/// </summary>
public class UnparseableEntityDedupTests
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

    private static IntegrationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<IntegrationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IntegrationDbContext(options);
    }

    [Fact]
    public async Task SameUnparseableBytes_CollectedTwice_DedupesToOneRow_WithRealFaultedAt()
    {
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        var firstCollectedAt = new DateTime(2026, 1, 1, 12, 0, 0);
        dateTimeProvider.ApplicationNow.Returns(firstCollectedAt);
        var collector = CreateCollector(dateTimeProvider);

        var body = "not json, not an envelope"u8.ToArray();
        var properties = new BasicProperties(); // no AMQP Timestamp — forces the collection-time path.
        var result = new BasicGetResult(1, false, "", "appraisal-sync_error", 0, properties, body);

        // First collection round for this broken broker message.
        var entity1 = collector.BuildUnparseableEntity(
            "appraisal-sync", FailedMessageKind.Error, result, new InvalidOperationException("boom"));

        entity1.FaultedAt.Should().NotBe(DateTime.UnixEpoch);
        entity1.FaultedAt.Should().Be(firstCollectedAt);

        await using var db = CreateInMemoryDbContext();
        db.FailedMessages.Add(entity1);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Redelivery of the EXACT SAME broker message, minutes later (its own ack was lost) — a fresh
        // build call gives real, different collection time, but the same content hashes to the same
        // synthetic MessageId.
        dateTimeProvider.ApplicationNow.Returns(firstCollectedAt.AddMinutes(5));
        var entity2 = collector.BuildUnparseableEntity(
            "appraisal-sync", FailedMessageKind.Error, result, new InvalidOperationException("boom"));

        entity2.MessageId.Should().Be(entity1.MessageId);
        entity2.FaultedAt.Should().NotBe(entity1.FaultedAt, "collection time is real and moves forward");

        var alreadyCollected = await FailedMessageCollectorService.AlreadyCollectedAsync(
            db, entity2, TestContext.Current.CancellationToken);

        alreadyCollected.Should().BeTrue("the same broken broker message must dedupe to the row already stored");

        // What CollectAsync actually does with that result: skip the insert entirely.
        var count = await db.FailedMessages.CountAsync(TestContext.Current.CancellationToken);
        count.Should().Be(1);
    }

    private static BasicGetResult ResultWith(byte[] body, Dictionary<string, object?>? headers, string? messageId = null) =>
        new(1, false, "", "appraisal-sync_error", 0,
            new BasicProperties { Headers = headers, MessageId = messageId }, body);

    [Fact]
    public async Task SameBody_DifferentHeaders_AreTwoDistinctRows_NotDedupedAway()
    {
        // Two DIFFERENT broken messages can share a body (e.g. two empty-body faults whose headers/exceptions
        // differ). Keying on the body alone made the second look like a redelivery of the first: it was acked
        // and lost.
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(new DateTime(2026, 1, 1, 12, 0, 0));
        var collector = CreateCollector(dateTimeProvider);
        var body = Array.Empty<byte>();

        var first = collector.BuildUnparseableEntity("appraisal-sync", FailedMessageKind.Error,
            ResultWith(body, new() { ["MT-Fault-ExceptionType"] = "A.Boom" }), new InvalidOperationException("boom"));
        var second = collector.BuildUnparseableEntity("appraisal-sync", FailedMessageKind.Error,
            ResultWith(body, new() { ["MT-Fault-ExceptionType"] = "B.Bang" }), new InvalidOperationException("boom"));

        await using var db = CreateInMemoryDbContext();
        db.FailedMessages.Add(first);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        second.MessageId.Should().NotBe(first.MessageId!.Value);
        (await FailedMessageCollectorService.AlreadyCollectedAsync(db, second, TestContext.Current.CancellationToken))
            .Should().BeFalse("a different message with an identical body is a NEW failure, not a redelivery");
    }

    [Fact]
    public async Task ExactRedelivery_SameBodyAndHeaders_InAnyHeaderOrder_DedupesToOneRow()
    {
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(new DateTime(2026, 1, 1, 12, 0, 0));
        var collector = CreateCollector(dateTimeProvider);
        var body = "not an envelope"u8.ToArray();

        var first = collector.BuildUnparseableEntity("appraisal-sync", FailedMessageKind.Error,
            ResultWith(body, new() { ["a"] = "1", ["b"] = "2" }), new InvalidOperationException("boom"));
        var redelivered = collector.BuildUnparseableEntity("appraisal-sync", FailedMessageKind.Error,
            ResultWith(body, new() { ["b"] = "2", ["a"] = "1" }), new InvalidOperationException("boom"));

        await using var db = CreateInMemoryDbContext();
        db.FailedMessages.Add(first);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        redelivered.MessageId.Should().Be(first.MessageId);
        (await FailedMessageCollectorService.AlreadyCollectedAsync(db, redelivered, TestContext.Current.CancellationToken))
            .Should().BeTrue();
    }

    [Fact]
    public void AmqpMessageId_WhenPresent_IsPreferredOverHeaders()
    {
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(new DateTime(2026, 1, 1, 12, 0, 0));
        var collector = CreateCollector(dateTimeProvider);
        var body = "x"u8.ToArray();

        var withHeaderX = collector.BuildUnparseableEntity("appraisal-sync", FailedMessageKind.Error,
            ResultWith(body, new() { ["h"] = "x" }, "msg-1"), new InvalidOperationException("boom"));
        var withHeaderY = collector.BuildUnparseableEntity("appraisal-sync", FailedMessageKind.Error,
            ResultWith(body, new() { ["h"] = "y" }, "msg-1"), new InvalidOperationException("boom"));
        var otherId = collector.BuildUnparseableEntity("appraisal-sync", FailedMessageKind.Error,
            ResultWith(body, new() { ["h"] = "x" }, "msg-2"), new InvalidOperationException("boom"));

        withHeaderY.MessageId.Should().Be(withHeaderX.MessageId, "the broker message-id identifies the message");
        otherId.MessageId.Should().NotBe(withHeaderX.MessageId!.Value);
    }

    [Fact]
    public void BuildUnparseableEntity_LongExceptionMessage_IsTruncatedToTheColumnLength()
    {
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(new DateTime(2026, 1, 1, 12, 0, 0));
        var collector = CreateCollector(dateTimeProvider);
        var result = new BasicGetResult(1, false, "", "appraisal-sync_error", 0, new BasicProperties(), "x"u8.ToArray());

        var entity = collector.BuildUnparseableEntity(
            "appraisal-sync", FailedMessageKind.Error, result,
            new InvalidOperationException(new string('x', FailedMessageConfiguration.ExceptionMessageMaxLength + 500)));

        entity.ExceptionMessage.Should().HaveLength(FailedMessageConfiguration.ExceptionMessageMaxLength);
    }
}
