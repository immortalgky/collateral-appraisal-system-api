using Common.Infrastructure.Metrics;
using FluentAssertions;

namespace Common.Tests.Metrics;

public class RequestDurationPercentileTests
{
    [Fact]
    public void P95Ms_NoDurations_ReturnsNull()
    {
        RequestDurationPercentile.P95Ms([]).Should().BeNull();
    }

    [Fact]
    public void P95Ms_SingleDuration_ReturnsThatDuration()
    {
        RequestDurationPercentile.P95Ms([0.25]).Should().Be(250);
    }

    [Fact]
    public void P95Ms_OneHundredEvenlySpacedDurations_ReturnsThe95thSmallest()
    {
        // 1ms..100ms — nearest-rank 95th percentile of 100 values is the 95th smallest (95ms).
        var durationsSeconds = Enumerable.Range(1, 100).Select(ms => ms / 1000.0);

        RequestDurationPercentile.P95Ms(durationsSeconds).Should().Be(95);
    }

    [Fact]
    public void P95Ms_TwentyEvenlySpacedDurations_ReturnsThe19thSmallest()
    {
        // ceil(20 * 0.95) = 19th smallest of 1ms..20ms = 19ms.
        var durationsSeconds = Enumerable.Range(1, 20).Select(ms => ms / 1000.0);

        RequestDurationPercentile.P95Ms(durationsSeconds).Should().Be(19);
    }

    [Fact]
    public void P95Ms_IsUnaffectedByInputOrder()
    {
        var shuffled = new[] { 0.05, 0.01, 0.09, 0.02, 0.03, 0.10, 0.04, 0.08, 0.06, 0.07 };

        RequestDurationPercentile.P95Ms(shuffled).Should().Be(RequestDurationPercentile.P95Ms(shuffled.Reverse()));
    }
}
