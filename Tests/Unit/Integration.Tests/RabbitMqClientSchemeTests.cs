using FluentAssertions;
using Integration.FailedMessages;

namespace Integration.Tests;

/// <summary>
/// Deployment-notes finding (docs/failed-messages/design.md): RabbitMQ.Client 7.1.2's
/// <c>ConnectionFactory.Uri</c> throws on a "rabbitmq"/"rabbitmqs" scheme even though MassTransit's own
/// <c>Host(Uri)</c> accepts it interchangeably with "amqp"/"amqps" — verified against both packages
/// directly. <see cref="FailedMessageCollectorService.ToRabbitMqClientScheme"/> converts so an operator
/// supplying the RabbitMQ-branded scheme doesn't take the collector down.
/// </summary>
public class RabbitMqClientSchemeTests
{
    [Theory]
    [InlineData("rabbitmq://user:pass@host1:5672/myvhost", "amqp://user:pass@host1:5672/myvhost")]
    [InlineData("rabbitmqs://host1:5671/", "amqps://host1:5671/")]
    public void ConvertsRabbitMqScheme_ToAmqpEquivalent(string input, string expected)
    {
        var result = FailedMessageCollectorService.ToRabbitMqClientScheme(new Uri(input));

        result.Should().Be(new Uri(expected));
    }

    [Theory]
    [InlineData("amqp://host1:5672/")]
    [InlineData("amqps://host1:5671/")]
    public void LeavesAlreadyValidSchemes_Unchanged(string input)
    {
        var uri = new Uri(input);

        FailedMessageCollectorService.ToRabbitMqClientScheme(uri).Should().Be(uri);
    }
}
