using FluentAssertions;
using Workflow.Domain.Committees;
using Xunit;

namespace Workflow.Tests.Domain;

/// <summary>
/// The committee's own satisfiability guards must count the members who actually VOTE, which is the
/// same subset <see cref="Workflow.Meetings.Domain.MeetingRosterEligibility"/> checks a meeting
/// roster against at release. Counting the Secretary here would let the admin screen accept a
/// threshold that the release gate then refuses — the two guards must not drift apart.
/// </summary>
public class CommitteeVotingThresholdTests
{
    [Fact]
    public void Update_FixedCountMajority_DoesNotCountTheSecretary()
    {
        // Four active members, one of them the Secretary => three voters. A FixedCount of 4 can
        // never be reached, even though the committee "has" four members.
        var committee = BuildCommitteeWithSecretary();

        var act = () => committee.Update("C", null, QuorumType.Fixed, 1,
            MajorityType.FixedCount, isActive: true, majorityValue: 4);

        act.Should().Throw<ArgumentException>().WithMessage("*3 voting member(s)*");
    }

    [Fact]
    public void Update_FixedCountMajority_AcceptsAThresholdTheVotersCanReach()
    {
        var committee = BuildCommitteeWithSecretary();

        var act = () => committee.Update("C", null, QuorumType.Fixed, 1,
            MajorityType.FixedCount, isActive: true, majorityValue: 3);

        act.Should().NotThrow();
    }

    [Fact]
    public void AddCondition_MinVotes_DoesNotCountTheSecretary()
    {
        var committee = BuildCommitteeWithSecretary();

        var act = () => committee.AddCondition(
            ConditionType.MinVotes, roleRequired: null, minVotesRequired: 4,
            priority: 1, description: null);

        act.Should().Throw<ArgumentException>().WithMessage("*3 voting member(s)*");
    }

    [Fact]
    public void AddCondition_MinVotes_AcceptsWhatTheVotersCanReach()
    {
        var committee = BuildCommitteeWithSecretary();

        var act = () => committee.AddCondition(
            ConditionType.MinVotes, roleRequired: null, minVotesRequired: 3,
            priority: 1, description: null);

        act.Should().NotThrow();
    }

    private static Committee BuildCommitteeWithSecretary()
    {
        var committee = Committee.Create("C", "C", null,
            QuorumType.Fixed, 1, MajorityType.Simple, VotingMode.WaitForAll);

        committee.AddMember("a", "Alice", CommitteeMemberPosition.Chairman);
        committee.AddMember("b", "Bob", CommitteeMemberPosition.Director);
        committee.AddMember("c", "Carol", CommitteeMemberPosition.UW);
        committee.AddMember("d", "Dan", CommitteeMemberPosition.Secretary);

        return committee;
    }
}
