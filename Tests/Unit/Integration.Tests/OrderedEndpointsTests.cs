using FluentAssertions;
using Integration.FailedMessages;

namespace Integration.Tests;

/// <summary>
/// <see cref="OrderedEndpoints"/> is the single source for the seven partitioned receive endpoints:
/// Bootstrapper/Api/Program.cs registers each <c>ReceiveEndpoint</c> from these constants, and the collector /
/// API read <see cref="OrderedEndpoints.Names"/>. A constant missing from <c>Names</c> would register an
/// endpoint the monitor then doesn't know is ordered.
/// </summary>
public class OrderedEndpointsTests
{
    [Fact]
    public void Names_ContainsEveryEndpointConstant_ExactlyOnce()
    {
        var constants = typeof(OrderedEndpoints)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        constants.Should().HaveCount(7);
        OrderedEndpoints.Names.Should().BeEquivalentTo(constants).And.OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("pma-sync-status", true)]
    [InlineData("appraisal-sync", true)]
    [InlineData("some-other-queue", false)]
    public void IsOrdered_MatchesTheSevenEndpoints(string queue, bool expected) =>
        OrderedEndpoints.IsOrdered(queue).Should().Be(expected);
}
