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
}
