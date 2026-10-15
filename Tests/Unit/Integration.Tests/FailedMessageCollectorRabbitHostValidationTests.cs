using FluentAssertions;
using Integration.FailedMessages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shared.Messaging.Services;
using Shared.Time;

namespace Integration.Tests;

/// <summary>
/// A missing or malformed RabbitMQ:Host used to surface as a raw
/// ArgumentNullException/UriFormatException from a bare <c>new Uri(...)</c> field initializer at DI
/// construction time, with no indication of which setting was wrong. Constructing the collector directly
/// (rather than through DI) is the cheapest way to prove the new, clearer failure — no host needed.
/// </summary>
public class FailedMessageCollectorRabbitHostValidationTests
{
    private static FailedMessageCollectorService CreateCollector(string? rabbitHost)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:Host"] = rabbitHost,
                ["RabbitMQ:Username"] = "guest",
                ["RabbitMQ:Password"] = "guest",
            })
            .Build();

        return new FailedMessageCollectorService(
            Substitute.For<IServiceScopeFactory>(),
            Substitute.For<IHttpClientFactory>(),
            Substitute.For<ILogger<FailedMessageCollectorService>>(),
            Substitute.For<IDateTimeProvider>(),
            Options.Create(new FailedMessagesOptions()),
            configuration,
            new ReceiveEndpointDiscoveryObserver());
    }

    [Fact]
    public void Construct_RabbitMqHostMissing_ThrowsWithClearMessage()
    {
        var act = () => CreateCollector(null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("RabbitMQ:Host must be an amqp(s):// or rabbitmq(s):// URI.");
    }

    [Theory]
    [InlineData("not a uri")]
    [InlineData("http://localhost:5672/")]
    public void Construct_RabbitMqHostMalformedOrWrongScheme_ThrowsWithClearMessage(string rabbitHost)
    {
        var act = () => CreateCollector(rabbitHost);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("RabbitMQ:Host must be an amqp(s):// or rabbitmq(s):// URI*");
    }

    [Theory]
    [InlineData("amqp://localhost:5672/")]
    [InlineData("amqps://localhost:5671/")]
    [InlineData("rabbitmq://localhost:5672/")]
    [InlineData("rabbitmqs://localhost:5671/")]
    public void Construct_RabbitMqHostValidScheme_DoesNotThrow(string rabbitHost)
    {
        var act = () => CreateCollector(rabbitHost);

        act.Should().NotThrow();
    }

    /// <summary>
    /// Management calls use the trimmed vhost ("cas"), but RabbitMQ.Client's own <c>ConnectionFactory.Uri</c>
    /// takes the raw path segment WITH its trailing slash ("cas/"), so the AMQP connection could target a
    /// different vhost than the Management API. The AMQP factory must carry the same normalised vhost.
    /// </summary>
    [Theory]
    [InlineData("amqp://mq:5672/cas/", "cas")]
    [InlineData("rabbitmq://mq:5672/cas/", "cas")]
    [InlineData("amqp://mq:5672/cas", "cas")]
    [InlineData("amqp://mq:5672/", "/")]
    [InlineData("amqp://mq:5672/%2F", "/")]
    public void CreateConnectionFactory_UsesTheSameNormalisedVhostAsManagementCalls(string rabbitHost, string expected)
    {
        var factory = CreateCollector(rabbitHost).CreateConnectionFactory();

        factory.VirtualHost.Should().Be(expected);
        factory.HostName.Should().Be("mq");
        factory.UserName.Should().Be("guest");
    }

    /// <summary>Root cause, pinned: left alone, RabbitMQ.Client keeps the trailing slash of the vhost segment.</summary>
    [Fact]
    public void RabbitMqClient_ConnectionFactoryUri_KeepsTheTrailingSlashOfTheVhostSegment()
    {
        new RabbitMQ.Client.ConnectionFactory { Uri = new Uri("amqp://mq:5672/cas/") }
            .VirtualHost.Should().Be("cas/");
    }
}
