using FluentAssertions;
using Workflow.Domain.Committees;
using Xunit;

namespace Workflow.Tests.Domain;

/// <summary>
/// Single source of truth for the committee majority rule (shared by Committee.HasMajority and
/// ApprovalActivity.CheckMajority). All thresholds count against the full committee.
/// </summary>
public class MajorityRuleTests
{
    [Theory]
    [InlineData(2, false)] // 2 of 5 is not a majority
    [InlineData(3, true)]  // 3 of 5 is
    public void Simple(int approve, bool expected) =>
        MajorityRule.IsMet(MajorityType.Simple, approve, 5).Should().Be(expected);

    [Theory]
    [InlineData(3, false)] // ceil(5 * 2/3) = 4
    [InlineData(4, true)]
    public void TwoThirds(int approve, bool expected) =>
        MajorityRule.IsMet(MajorityType.TwoThirds, approve, 5).Should().Be(expected);

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    public void Unanimous(int approve, bool expected) =>
        MajorityRule.IsMet(MajorityType.Unanimous, approve, 5).Should().Be(expected);

    [Fact]
    public void EvenCommittee_SimpleRequiresStrictMajority() =>
        // 2 of 4 is exactly half — not a majority.
        MajorityRule.IsMet(MajorityType.Simple, 2, 4).Should().BeFalse();
}
