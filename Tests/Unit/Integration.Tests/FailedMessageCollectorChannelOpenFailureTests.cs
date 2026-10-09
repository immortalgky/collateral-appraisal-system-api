using FluentAssertions;
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
/// Collect opens one channel per fault queue. A channel-open failure for ONE queue (e.g. channel_max
/// reached) must not abort the round for every queue after it — unless the connection itself is closed,
/// in which case the rest would fail too and the loop stops.
/// </summary>
public class FailedMessageCollectorChannelOpenFailureTests
{
    private static (FailedMessageCollectorService Collector, IntegrationDbContext Db) CreateCollector()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<IntegrationDbContext>(o => o.UseInMemoryDatabase(dbName));
        var provider = services.BuildServiceProvider();

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(new DateTime(2026, 1, 1, 9, 0, 0));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:Host"] = "amqp://localhost:5672/",
                ["RabbitMQ:Username"] = "guest",
                ["RabbitMQ:Password"] = "guest",
            })
            .Build();

        var collector = new FailedMessageCollectorService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Substitute.For<IHttpClientFactory>(),
            Substitute.For<ILogger<FailedMessageCollectorService>>(),
            dateTimeProvider,
            Options.Create(new FailedMessagesOptions()),
            configuration,
            new ReceiveEndpointDiscoveryObserver());

        return (collector, provider.CreateScope().ServiceProvider.GetRequiredService<IntegrationDbContext>());
    }

    private static IChannel EmptyChannel()
    {
        var channel = Substitute.For<IChannel>();
        channel.BasicGetAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BasicGetResult?>(null));
        return channel;
    }

    private static BasicGetResult ParseableFault(string queue) =>
        new(1, false, "", queue, 0,
            new BasicProperties
            {
                ContentType = "application/json",
                Headers = new Dictionary<string, object?>
                {
                    ["MT-Fault-ExceptionType"] = "System.TimeoutException",
                    ["MT-Fault-Message"] = "boom",
                }
            },
            "{}"u8.ToArray());

    [Fact]
    public async Task Collect_ChannelOpenFailsForOneQueue_StillCollectsTheNextQueue()
    {
        var (collector, db) = CreateCollector();
        await using var _ = db;

        var queue3Channel = Substitute.For<IChannel>();
        queue3Channel.BasicGetAsync("q3_error", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BasicGetResult?>(ParseableFault("q3_error")),
                Task.FromResult<BasicGetResult?>(null));

        var calls = 0;
        var connection = Substitute.For<IConnection>();
        connection.IsOpen.Returns(true);
        connection.CreateChannelAsync(Arg.Any<CreateChannelOptions?>(), Arg.Any<CancellationToken>())
            .Returns(_ => ++calls switch
            {
                1 => Task.FromResult(EmptyChannel()),
                2 => Task.FromException<IChannel>(new InvalidOperationException("channel_max reached")),
                _ => Task.FromResult(queue3Channel)
            });

        await collector.CollectAsync(connection, ["q1_error", "q2_error", "q3_error"],
            TestContext.Current.CancellationToken);

        calls.Should().Be(3, "a channel is opened for every queue, including the one after the failure");
        await queue3Channel.Received(1).BasicAckAsync(Arg.Any<ulong>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Collect_ChannelOpenFailsAndConnectionIsClosed_StopsTheLoop()
    {
        var (collector, db) = CreateCollector();
        await using var _ = db;

        var calls = 0;
        var connection = Substitute.For<IConnection>();
        connection.IsOpen.Returns(false);
        connection.CreateChannelAsync(Arg.Any<CreateChannelOptions?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                calls++;
                return Task.FromException<IChannel>(new InvalidOperationException("connection closed"));
            });

        await collector.CollectAsync(connection, ["q1_error", "q2_error", "q3_error"],
            TestContext.Current.CancellationToken);

        calls.Should().Be(1, "a closed connection fails every remaining queue too — stop instead of retrying each");
    }
}
