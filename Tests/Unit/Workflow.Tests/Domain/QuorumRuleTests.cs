using FluentAssertions;
using Workflow.Domain.Committees;
using Xunit;

namespace Workflow.Tests.Domain;

/// <summary>
/// The approval engine and the meeting release gate both ask this rule how many votes a round
/// needs, so a roster accepted at release is one the round can actually resolve with.
/// </summary>
public class QuorumRuleTests
{
    [Theory]
    [InlineData(3, 5, 3)]
    [InlineData(3, 2, 3)]   // a Fixed quorum ignores the member count — this is the case that strands a round
    [InlineData(1, 0, 1)]
    public void Required_Fixed_IsTheConfiguredValue(int value, int memberCount, int expected)
        => QuorumRule.Required(QuorumType.Fixed, value, memberCount).Should().Be(expected);

    [Theory]
    [InlineData(50, 4, 2)]
    [InlineData(50, 5, 3)]   // rounds up
    [InlineData(100, 3, 3)]
    [InlineData(60, 0, 0)]
    public void Required_Percentage_RoundsUpAgainstTheMemberCount(int value, int memberCount, int expected)
        => QuorumRule.Required(QuorumType.Percentage, value, memberCount).Should().Be(expected);

    [Fact]
    public void Required_Percentage_NeverExceedsTheMemberCount()
    {
        // Why only a Fixed quorum can make a roster ineligible at release time.
        for (var memberCount = 1; memberCount <= 20; memberCount++)
            QuorumRule.Required(QuorumType.Percentage, 100, memberCount)
                .Should().BeLessThanOrEqualTo(memberCount);
    }

    [Theory]
    [InlineData("Fixed", 2, 5, 2)]
    [InlineData("fixed", 2, 5, 2)]
    [InlineData("Percentage", 50, 4, 2)]
    [InlineData("percentage", 50, 4, 2)]
    public void Required_StringOverload_ParsesTheRoundTrippedTypeName(
        string type, int value, int memberCount, int expected)
        => QuorumRule.Required(type, value, memberCount).Should().Be(expected);

    [Fact]
    public void Required_UnknownTypeName_FallsBackToRequiringEveryMember()
        => QuorumRule.Required("nonsense", 2, 4).Should().Be(4);
}
