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

    // -- FixedCount: an absolute threshold, independent of member count --

    [Theory]
    [InlineData(2, 3, false)]
    [InlineData(3, 3, true)]  // the business case: 3 approvals out of 7 members is enough
    [InlineData(4, 3, true)]  // above the threshold still passes
    public void FixedCount_IsMetAtOrAboveTheThreshold(int approve, int value, bool expected) =>
        MajorityRule.IsMet(MajorityType.FixedCount, approve, totalMembers: 7, value).Should().Be(expected);

    [Fact]
    public void FixedCount_IgnoresMemberCount()
    {
        // Same 3 approvals, wildly different committee sizes — all met. This is exactly what
        // distinguishes it from every proportional rule above, where 3 of 20 would fail.
        foreach (var members in new[] { 3, 7, 20, 100 })
            MajorityRule.IsMet(MajorityType.FixedCount, 3, members, 3).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void FixedCount_NonPositiveThreshold_IsNeverMet(int value)
    {
        // Without the value > 0 guard, `approveCount >= 0` would approve on zero votes.
        // Committee.Create/Update reject this up front; this is the defence behind that.
        MajorityRule.IsMet(MajorityType.FixedCount, 0, 5, value).Should().BeFalse();
        MajorityRule.IsMet(MajorityType.FixedCount, 5, 5, value).Should().BeFalse();
    }

    [Fact]
    public void FixedCount_ThresholdAboveMemberCount_CanNeverBeMet() =>
        // Unreachable by construction — every member approving still falls short. Committee.Update
        // and the ApprovalActivity round-start guard stop a round ever opening on this.
        MajorityRule.IsMet(MajorityType.FixedCount, approveCount: 4, totalMembers: 4, value: 5)
            .Should().BeFalse();

    [Fact]
    public void ProportionalTypes_IgnoreTheValue()
    {
        // The value exists only for FixedCount; supplying one must not perturb the others.
        MajorityRule.IsMet(MajorityType.Simple, 2, 5, value: 2).Should().BeFalse();
        MajorityRule.IsMet(MajorityType.Unanimous, 5, 5, value: 99).Should().BeTrue();
    }
}
