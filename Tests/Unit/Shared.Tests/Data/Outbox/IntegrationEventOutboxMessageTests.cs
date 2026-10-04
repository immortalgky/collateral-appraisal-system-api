using FluentAssertions;
using Shared.Data.Outbox;

namespace Shared.Tests.Data.Outbox;

public class IntegrationEventOutboxMessageTests
{
    private static IntegrationEventOutboxMessage CreateMessage() =>
        IntegrationEventOutboxMessage.Create("Some.EventType", "{}", DateTime.Now);

    [Fact]
    public void MarkAsProcessing_sets_ProcessingStartedAt()
    {
        var message = CreateMessage();
        var now = DateTime.Now;

        message.MarkAsProcessing(now);

        message.Status.Should().Be(OutboxMessageStatus.Processing);
        message.ProcessingStartedAt.Should().Be(now);
    }

    [Fact]
    public void MarkAsProcessed_clears_ProcessingStartedAt()
    {
        var message = CreateMessage();
        message.MarkAsProcessing(DateTime.Now);

        message.MarkAsProcessed(DateTime.Now);

        message.ProcessingStartedAt.Should().BeNull();
    }

    [Fact]
    public void IncrementRetryCount_clears_ProcessingStartedAt()
    {
        var message = CreateMessage();
        message.MarkAsProcessing(DateTime.Now);

        message.IncrementRetryCount("boom", maxRetries: 5);

        message.ProcessingStartedAt.Should().BeNull();
        message.Status.Should().Be(OutboxMessageStatus.Pending);
    }

    [Fact]
    public void IncrementRetryCount_past_max_retries_keeps_ProcessingStartedAt_as_the_last_claim()
    {
        var message = CreateMessage();
        var claimedAt = DateTime.Now;
        message.MarkAsProcessing(claimedAt);

        message.IncrementRetryCount("boom", maxRetries: 0);

        message.Status.Should().Be(OutboxMessageStatus.Failed);
        message.ProcessingStartedAt.Should().Be(claimedAt,
            "a Failed row's last claim is what OutboxCleanupJob ages it by, so a resent row that fails again is not purged by its original age");
    }

    [Fact]
    public void MarkAsFailed_keeps_ProcessingStartedAt_as_the_last_claim()
    {
        var message = CreateMessage();
        var claimedAt = DateTime.Now;
        message.MarkAsProcessing(claimedAt);

        message.MarkAsFailed("disallowed type");

        message.Status.Should().Be(OutboxMessageStatus.Failed);
        message.ProcessingStartedAt.Should().Be(claimedAt);
    }

    [Fact]
    public void MarkAsPending_clears_ProcessingStartedAt()
    {
        var message = CreateMessage();
        message.MarkAsProcessing(DateTime.Now);

        message.MarkAsPending();

        message.Status.Should().Be(OutboxMessageStatus.Pending);
        message.ProcessingStartedAt.Should().BeNull();
    }
}
