using FluentAssertions;
using Integration.Application.Features.FailedMessages;
using Integration.Domain.FailedMessages;
using Shared.Messaging.Filters;

namespace Integration.Tests;

/// <summary>The retry gate (<see cref="FailedMessageRetryPolicy.IsTooSoon"/>) and the API's
/// <c>retryAvailableAt</c> projection must agree, and both measure the stale-claim window from
/// max(FaultedAt, CollectedAt).</summary>
public class FailedMessageRetryPolicyTests
{
    private static readonly DateTime Published = new(2026, 1, 1, 9, 0, 0);

    [Fact]
    public void RetryAvailableAt_UsesTheLaterOfFaultedAtAndCollectedAt()
    {
        FailedMessageRetryPolicy.RetryAvailableAt(Published, Published.AddHours(1))
            .Should().Be(Published.AddHours(1) + InboxGuardPolicy.StaleThreshold);
        FailedMessageRetryPolicy.RetryAvailableAt(Published.AddHours(1), Published)
            .Should().Be(Published.AddHours(1) + InboxGuardPolicy.StaleThreshold);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void IsTooSoon_AndRetryAvailableAtOrNull_AgreeAroundTheWindowEdge(int secondsFromEdge)
    {
        var collectedAt = Published.AddHours(1);
        var now = FailedMessageRetryPolicy.RetryAvailableAt(Published, collectedAt).AddSeconds(secondsFromEdge);

        var tooSoon = FailedMessageRetryPolicy.IsTooSoon(Published, collectedAt, now);
        var availableAt = FailedMessageRetryPolicy.RetryAvailableAtOrNull(
            FailedMessageStatus.Pending, Published, collectedAt, now);

        (availableAt is not null).Should().Be(tooSoon);
    }

    [Fact]
    public void RetryAvailableAtOrNull_NotPending_IsNull() =>
        FailedMessageRetryPolicy.RetryAvailableAtOrNull(
                FailedMessageStatus.Retried, Published, Published, Published)
            .Should().BeNull();
}
