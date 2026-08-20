using FluentAssertions;
using Workflow.Domain.Committees;
using Xunit;

namespace Workflow.Tests.Domain;

/// <summary>
/// Verifies <see cref="Committee.HasMajority"/> counts approvals against the FULL committee
/// (totalMembers), not the votes cast.
/// </summary>
public class CommitteeMajorityTests
{
    private static Committee CommitteeWith(MajorityType majority) =>
        Committee.Create("C", "C", null, QuorumType.Fixed, 1, majority);

    [Theory]
    [InlineData(2, false)] // 2 of 5 is not a majority of all members
    [InlineData(3, true)]  // 3 of 5 is
    public void Simple_CountsAgainstAllMembers(int approveCount, bool expected)
    {
        // totalVotes deliberately equals approveCount to prove the denominator is members, not votes.
        CommitteeWith(MajorityType.Simple)
            .HasMajority(approveCount, totalVotes: approveCount, totalMembers: 5)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(3, false)] // ceil(5 * 2/3) = 4, so 3 is short
    [InlineData(4, true)]
    public void TwoThirds_CountsAgainstAllMembers(int approveCount, bool expected)
    {
        CommitteeWith(MajorityType.TwoThirds)
            .HasMajority(approveCount, totalVotes: approveCount, totalMembers: 5)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    public void Unanimous_RequiresAllMembers(int approveCount, bool expected)
    {
        CommitteeWith(MajorityType.Unanimous)
            .HasMajority(approveCount, totalVotes: approveCount, totalMembers: 5)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public void FixedCount_UsesTheCommitteesConfiguredValue(int approveCount, bool expected)
    {
        // 3 approvals is enough on a 7-member committee — where Simple would demand 4.
        Committee.Create("C", "C", null, QuorumType.Fixed, 1,
                MajorityType.FixedCount, VotingMode.WaitForAll, majorityValue: 3)
            .HasMajority(approveCount, totalVotes: approveCount, totalMembers: 7)
            .Should().Be(expected);
    }

    [Fact]
    public void Create_FixedCountWithoutAValue_IsRejected()
    {
        // Would otherwise persist a committee whose every round approves on zero votes.
        var act = () => Committee.Create("C", "C", null, QuorumType.Fixed, 1,
            MajorityType.FixedCount, VotingMode.WaitForAll, majorityValue: 0);

        act.Should().Throw<ArgumentException>().WithMessage("*MajorityValue*");
    }

    [Fact]
    public void Update_FixedCountAboveActiveMemberCount_IsRejected()
    {
        var committee = Committee.Create("C", "C", null, QuorumType.Fixed, 1, MajorityType.Simple);
        committee.AddMember("alice", "Alice", CommitteeMemberPosition.Chairman);
        committee.AddMember("bob", "Bob", CommitteeMemberPosition.UW);

        var act = () => committee.Update("C", null, QuorumType.Fixed, 1,
            MajorityType.FixedCount, isActive: true, VotingMode.WaitForAll, majorityValue: 3);

        act.Should().Throw<ArgumentException>().WithMessage("*exceeds*2 voting member(s)*");
    }

    [Fact]
    public void Update_FixedCountAtTheMemberCount_IsAllowed()
    {
        var committee = Committee.Create("C", "C", null, QuorumType.Fixed, 1, MajorityType.Simple);
        committee.AddMember("alice", "Alice", CommitteeMemberPosition.Chairman);
        committee.AddMember("bob", "Bob", CommitteeMemberPosition.UW);

        committee.Update("C", null, QuorumType.Fixed, 1,
            MajorityType.FixedCount, isActive: true, VotingMode.WaitForAll, majorityValue: 2);

        committee.MajorityValue.Should().Be(2);
        committee.MajorityType.Should().Be(MajorityType.FixedCount);
    }
}
