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
/// The Unparseable fallback is the last line of defence for a message that cannot be parsed, so it must
/// never throw itself: a throw there (it used to, on a whitespace-only exception message, which
/// FailedMessage.Create rejects) escaped the parse-fallback catch and aborted Collect for that queue and
/// every queue after it, on every round, forever.
/// </summary>
public class UnparseableEntityBuilderTests
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

    private static IDateTimeProvider Clock()
    {
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(new DateTime(2026, 1, 1, 12, 0, 0));
        return dateTimeProvider;
    }

    private static BasicGetResult Result() =>
        new(1, false, "", "appraisal-sync_error", 0, new BasicProperties(), "x"u8.ToArray());

    private sealed class WhitespaceMessageException() : Exception("   \t\r\n ");

    [Fact]
    public void BuildUnparseableEntity_WhitespaceOnlyExceptionMessage_StillBuildsAnEntity()
    {
        var collector = CreateCollector(Clock());

        var entity = collector.BuildUnparseableEntity(
            "appraisal-sync", FailedMessageKind.Error, Result(), new WhitespaceMessageException());

        entity.ExceptionType.Should().Be(FailedMessageCollectorService.UnparseableExceptionType);
        entity.ExceptionMessage.Should().Be("Unparseable");
    }

    [Theory]
    [InlineData("", FailedMessageKind.Error)]
    [InlineData("   ", FailedMessageKind.Error)]
    [InlineData("appraisal-sync", " ")]
    public void BuildUnparseableEntity_BlankSourceQueueOrKind_StillBuildsAnEntity(string sourceQueue, string kind)
    {
        var collector = CreateCollector(Clock());

        var act = () => collector.BuildUnparseableEntity(sourceQueue, kind, Result(), new InvalidOperationException("boom"));

        act.Should().NotThrow();
    }

    [Fact]
    public async Task ProcessOneMessageAsync_ParseAndFallbackBuildBothThrow_NacksThatOneMessage_DoesNotThrow()
    {
        var collector = CreateCollector(Clock());
        await using var db = new IntegrationDbContext(new DbContextOptionsBuilder<IntegrationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var channel = Substitute.For<IChannel>();

        // No BasicProperties at all: BuildEntity throws, and so does the fallback builder (it reads the AMQP
        // timestamp off them) — the double failure the old code let escape into CollectAsync's queue loop.
        var result = new BasicGetResult(7, false, "", "appraisal-sync_error", 0, null!, "x"u8.ToArray());

        var keepGoing = await collector.ProcessOneMessageAsync(
            db, channel, "appraisal-sync", FailedMessageKind.Error, "appraisal-sync_error", result,
            TestContext.Current.CancellationToken);

        keepGoing.Should().BeFalse("only this queue stops");
        await channel.Received(1).BasicNackAsync(7, false, true, Arg.Any<CancellationToken>());
        await channel.DidNotReceive().BasicAckAsync(
            Arg.Any<ulong>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        (await db.FailedMessages.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }
}
