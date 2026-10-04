using FluentAssertions;
using Integration.FailedMessages;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Integration.Tests;

/// <summary>
/// DiscoverViaPassiveDeclareAsync must cache a queue as "missing" only for a
/// REAL NOT_FOUND from the broker (AMQP reply code 404) — any other
/// <see cref="OperationInterruptedException"/> (a different protocol-level error) says nothing about
/// whether this specific queue exists.
/// </summary>
public class FailedMessageQueueNotFoundClassificationTests
{
    private static OperationInterruptedException BuildException(ushort replyCode, string replyText) =>
        new(new ShutdownEventArgs(ShutdownInitiator.Peer, replyCode, replyText, cause: null!, CancellationToken.None));

    [Fact]
    public void IsQueueNotFound_ReplyCode404_ReturnsTrue()
    {
        var ex = BuildException(404, "NOT_FOUND - no queue 'x' in vhost '/'");

        FailedMessageCollectorService.IsQueueNotFound(ex).Should().BeTrue();
    }

    [Theory]
    [InlineData((ushort)403, "ACCESS_REFUSED")]
    [InlineData((ushort)320, "CONNECTION_FORCED")]
    [InlineData((ushort)530, "NOT_ALLOWED")]
    public void IsQueueNotFound_AnyOtherReplyCode_ReturnsFalse(ushort replyCode, string replyText)
    {
        var ex = BuildException(replyCode, replyText);

        FailedMessageCollectorService.IsQueueNotFound(ex).Should().BeFalse();
    }
}
