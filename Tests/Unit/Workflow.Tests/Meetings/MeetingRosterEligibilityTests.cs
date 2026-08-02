using FluentAssertions;
using Workflow.Domain.Committees;
using Workflow.Meetings.Domain;
using Xunit;

namespace Workflow.Tests.Meetings;

/// <summary>
/// The roster replaces the committee's members in the approval round, but quorum and the
/// committee's approval conditions still govern that round. These tests pin the combinations
/// that would otherwise open a round that can never resolve.
/// </summary>
public class MeetingRosterEligibilityTests
{
    [Fact]
    public void Check_RosterMeetsFixedQuorum_NoFailures()
    {
        var committee = BuildCommittee(QuorumType.Fixed, 3);

        var failures = MeetingRosterEligibility.Check(
            Roster(("a", CommitteeMemberPosition.Chairman),
                   ("b", CommitteeMemberPosition.Member),
                   ("c", CommitteeMemberPosition.Member)),
            committee);

        failures.Should().BeEmpty();
    }

    [Fact]
    public void Check_RosterBelowFixedQuorum_Fails()
    {
        var committee = BuildCommittee(QuorumType.Fixed, 3);

        var failures = MeetingRosterEligibility.Check(
            Roster(("a", CommitteeMemberPosition.Chairman),
                   ("b", CommitteeMemberPosition.Member)),
            committee);

        failures.Should().ContainSingle()
            .Which.Should().Be("2 member(s) but quorum requires 3");
    }

    [Fact]
    public void Check_PercentageQuorum_IsAlwaysSatisfiableByTheRosterItself()
    {
        // Percentage is taken of the members the round runs with — the roster — so it can never
        // demand more people than are on it. Only a Fixed quorum can strand a round.
        var committee = BuildCommittee(QuorumType.Percentage, 100);

        var failures = MeetingRosterEligibility.Check(
            Roster(("a", CommitteeMemberPosition.Chairman)), committee);

        failures.Should().BeEmpty();
    }

    [Fact]
    public void Check_RosterMissingRequiredRole_Fails()
    {
        var committee = BuildCommittee(QuorumType.Fixed, 2);
        committee.AddCondition(ConditionType.RoleRequired, nameof(CommitteeMemberPosition.UW),
            minVotesRequired: null, priority: 1, description: "UW must approve");

        var failures = MeetingRosterEligibility.Check(
            Roster(("a", CommitteeMemberPosition.Chairman),
                   ("b", CommitteeMemberPosition.Member)),
            committee);

        failures.Should().ContainSingle()
            .Which.Should().Be("no member holds the required role UW");
    }

    [Fact]
    public void Check_RosterHoldsRequiredRole_NoFailures()
    {
        var committee = BuildCommittee(QuorumType.Fixed, 2);
        committee.AddCondition(ConditionType.RoleRequired, nameof(CommitteeMemberPosition.UW),
            null, 1, "UW must approve");

        var failures = MeetingRosterEligibility.Check(
            Roster(("a", CommitteeMemberPosition.Chairman),
                   ("b", CommitteeMemberPosition.UW)),
            committee);

        failures.Should().BeEmpty();
    }

    [Fact]
    public void Check_InactiveCondition_IsIgnored()
    {
        var committee = BuildCommittee(QuorumType.Fixed, 1);
        var condition = committee.AddCondition(ConditionType.RoleRequired,
            nameof(CommitteeMemberPosition.UW), null, 1, "UW must approve");
        condition.Deactivate();

        var failures = MeetingRosterEligibility.Check(
            Roster(("a", CommitteeMemberPosition.Chairman)), committee);

        failures.Should().BeEmpty();
    }

    [Fact]
    public void Check_MinVotesExceedsRosterSize_Fails()
    {
        var committee = BuildCommittee(QuorumType.Fixed, 1);
        committee.AddCondition(ConditionType.MinVotes, roleRequired: null,
            minVotesRequired: 3, priority: 1, description: "at least 3 approvals");

        var failures = MeetingRosterEligibility.Check(
            Roster(("a", CommitteeMemberPosition.Chairman),
                   ("b", CommitteeMemberPosition.Member)),
            committee);

        failures.Should().ContainSingle()
            .Which.Should().Be("3 approve vote(s) required but the roster has only 2 member(s)");
    }

    [Fact]
    public void Check_MultipleUnsatisfiableRules_ReportsAllOfThem()
    {
        var committee = BuildCommittee(QuorumType.Fixed, 3);
        committee.AddCondition(ConditionType.RoleRequired, nameof(CommitteeMemberPosition.UW),
            null, 1, "UW must approve");

        var failures = MeetingRosterEligibility.Check(
            Roster(("a", CommitteeMemberPosition.Chairman)), committee);

        failures.Should().BeEquivalentTo(
        [
            "1 member(s) but quorum requires 3",
            "no member holds the required role UW"
        ]);
    }

    [Fact]
    public void Check_EmptyRoster_FailsQuorum()
    {
        var committee = BuildCommittee(QuorumType.Fixed, 1);

        var failures = MeetingRosterEligibility.Check([], committee);

        failures.Should().ContainSingle()
            .Which.Should().Be("0 member(s) but quorum requires 1");
    }

    // -- Helpers --

    private static Committee BuildCommittee(QuorumType quorumType, int quorumValue)
        => Committee.Create("Committee With Meeting", MeetingCommittee.WithMeetingCode, null,
            quorumType, quorumValue, MajorityType.Unanimous, VotingMode.WaitForAll);

    private static List<MeetingMember> Roster(params (string UserId, CommitteeMemberPosition Position)[] members)
        => members
            .Select(m => MeetingMember.CreateManual(Guid.NewGuid(), m.UserId, m.UserId, m.Position))
            .ToList();
}
