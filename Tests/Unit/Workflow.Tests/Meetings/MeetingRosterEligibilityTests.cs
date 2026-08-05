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
                   ("b", CommitteeMemberPosition.Director),
                   ("c", CommitteeMemberPosition.Director)),
            committee);

        failures.Should().BeEmpty();
    }

    [Fact]
    public void Check_RosterBelowFixedQuorum_Fails()
    {
        var committee = BuildCommittee(QuorumType.Fixed, 3);

        var failures = MeetingRosterEligibility.Check(
            Roster(("a", CommitteeMemberPosition.Chairman),
                   ("b", CommitteeMemberPosition.Director)),
            committee);

        failures.Should().ContainSingle()
            .Which.Should().Be("2 voting member(s) but quorum requires 3");
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
                   ("b", CommitteeMemberPosition.Director)),
            committee);

        failures.Should().ContainSingle()
            .Which.Should().Be("no voting member holds the required role UW");
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
                   ("b", CommitteeMemberPosition.Director)),
            committee);

        failures.Should().ContainSingle()
            .Which.Should().Be("3 approve vote(s) required but the roster has only 2 voting member(s)");
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
            "1 voting member(s) but quorum requires 3",
            "no voting member holds the required role UW"
        ]);
    }

    [Fact]
    public void Check_EmptyRoster_Fails()
    {
        var committee = BuildCommittee(QuorumType.Fixed, 1);

        var failures = MeetingRosterEligibility.Check([], committee);

        failures.Should().ContainSingle()
            .Which.Should().Be("the meeting has no members");
    }

    [Fact]
    public void Check_EmptyRoster_FailsEvenWhenQuorumWouldAcceptIt()
    {
        // The case a quorum check alone cannot catch: a Percentage of zero members is zero, so
        // `roster.Count < requiredQuorum` is 0 < 0 = false. Released with an empty roster,
        // ApprovalActivity's `overrideMembers.Count > 0` switch reads it as "no override" and
        // silently runs the round with the COMMITTEE's members instead.
        var committee = BuildCommittee(QuorumType.Percentage, 100);

        var failures = MeetingRosterEligibility.Check([], committee);

        failures.Should().ContainSingle()
            .Which.Should().Be("the meeting has no members");
    }

    [Fact]
    public void Check_MemberWithNoMatchingUser_Fails()
    {
        var committee = BuildCommittee(QuorumType.Fixed, 2);

        var failures = MeetingRosterEligibility.Check(
            Roster(("alice", CommitteeMemberPosition.Chairman),
                   ("ghost", CommitteeMemberPosition.UW)),
            committee,
            knownUsernames: Known("alice"));

        failures.Should().ContainSingle()
            .Which.Should().Be("no such user: ghost");
    }

    [Fact]
    public void Check_UnresolvedMembers_AreReportedTogether()
    {
        var committee = BuildCommittee(QuorumType.Fixed, 1);

        var failures = MeetingRosterEligibility.Check(
            Roster(("alice", CommitteeMemberPosition.Chairman),
                   ("ghost", CommitteeMemberPosition.UW),
                   ("phantom", CommitteeMemberPosition.Director)),
            committee,
            knownUsernames: Known("alice"));

        failures.Should().ContainSingle()
            .Which.Should().Be("no such user: ghost, phantom");
    }

    [Fact]
    public void Check_KnownUsernamesMatchCaseInsensitively()
    {
        // Member usernames are compared case-insensitively everywhere else in the approval path
        // (ApprovalActivity's voter lookup, RoleRequired matching), so this must agree.
        var committee = BuildCommittee(QuorumType.Fixed, 1);

        var failures = MeetingRosterEligibility.Check(
            Roster(("Alice", CommitteeMemberPosition.Chairman)),
            committee,
            knownUsernames: Known("alice"));

        failures.Should().BeEmpty();
    }

    [Fact]
    public void Check_WithoutKnownUsernames_SkipsTheUserCheck()
    {
        // The domain cannot resolve users itself; callers that do not supply the set opt out.
        var committee = BuildCommittee(QuorumType.Fixed, 1);

        var failures = MeetingRosterEligibility.Check(
            Roster(("ghost", CommitteeMemberPosition.Chairman)), committee);

        failures.Should().BeEmpty();
    }

    [Fact]
    public void Check_RosterSmallerThanFixedCountMajority_Fails()
    {
        // The roster replaces the committee's members but the majority rule still comes from the
        // committee, so a fixed threshold larger than the roster can never be reached.
        var committee = Committee.Create("Committee With Meeting", MeetingCommittee.WithMeetingCode,
            null, QuorumType.Fixed, 1, MajorityType.FixedCount, VotingMode.Quorum, majorityValue: 3);

        var failures = MeetingRosterEligibility.Check(
            Roster(("a", CommitteeMemberPosition.Chairman),
                   ("b", CommitteeMemberPosition.UW)),
            committee);

        failures.Should().ContainSingle()
            .Which.Should().Be("3 approve vote(s) required but the roster has only 2 voting member(s)");
    }

    [Fact]
    public void Check_RosterMeetsFixedCountMajority_NoFailures()
    {
        var committee = Committee.Create("Committee With Meeting", MeetingCommittee.WithMeetingCode,
            null, QuorumType.Fixed, 1, MajorityType.FixedCount, VotingMode.Quorum, majorityValue: 2);

        var failures = MeetingRosterEligibility.Check(
            Roster(("a", CommitteeMemberPosition.Chairman),
                   ("b", CommitteeMemberPosition.UW)),
            committee);

        failures.Should().BeEmpty();
    }

    [Fact]
    public void Check_ProportionalMajority_IsAlwaysSatisfiableByTheRosterItself()
    {
        // Unanimous of a 1-member roster is 1 — proportional rules are taken of the roster, so
        // only FixedCount can be unreachable. Same reasoning as a Percentage quorum.
        var committee = BuildCommittee(QuorumType.Fixed, 1);

        var failures = MeetingRosterEligibility.Check(
            Roster(("a", CommitteeMemberPosition.Chairman)), committee);

        failures.Should().BeEmpty();
    }

    // -- The secretary attends but does not vote --

    [Fact]
    public void Check_SecretaryDoesNotCountTowardQuorum()
    {
        // The exact stall this guard exists for: three people on the roster clears a quorum of 3,
        // but release hands the round only the two who vote, so it could never reach quorum.
        var committee = BuildCommittee(QuorumType.Fixed, 3);

        var failures = MeetingRosterEligibility.Check(
            Roster(("a", CommitteeMemberPosition.Chairman),
                   ("b", CommitteeMemberPosition.Secretary),
                   ("c", CommitteeMemberPosition.UW)),
            committee);

        failures.Should().ContainSingle()
            .Which.Should().Be("2 voting member(s) but quorum requires 3");
    }

    [Fact]
    public void Check_RosterOfOnlySecretaries_Fails()
    {
        // Non-empty, so the empty-roster guard does not catch it, yet it yields no approvers at all.
        // Released, ApprovalActivity's `overrideMembers.Count > 0` switch would read the empty list
        // as "no override" and silently run the round with the COMMITTEE's members.
        var committee = BuildCommittee(QuorumType.Percentage, 100);

        var failures = MeetingRosterEligibility.Check(
            Roster(("a", CommitteeMemberPosition.Secretary)), committee);

        failures.Should().ContainSingle()
            .Which.Should().Be("the meeting has no voting members (the secretary does not vote)");
    }

    [Fact]
    public void Check_SecretaryCannotSatisfyARequiredRole()
    {
        // No vote can ever carry the Secretary role, so a roster that leans on them for a
        // RoleRequired condition is unsatisfiable even though someone "holds" the position.
        var committee = BuildCommittee(QuorumType.Fixed, 1);
        committee.AddCondition(ConditionType.RoleRequired, nameof(CommitteeMemberPosition.UW),
            null, 1, "UW must approve");

        var failures = MeetingRosterEligibility.Check(
            Roster(("a", CommitteeMemberPosition.Chairman),
                   ("b", CommitteeMemberPosition.Secretary)),
            committee);

        failures.Should().ContainSingle()
            .Which.Should().Be("no voting member holds the required role UW");
    }

    [Fact]
    public void Check_SecretaryAlongsideEnoughVoters_NoFailures()
    {
        // The Secretary is not a problem in itself — only when the voters left behind fall short.
        var committee = BuildCommittee(QuorumType.Fixed, 2);
        committee.AddCondition(ConditionType.RoleRequired, nameof(CommitteeMemberPosition.UW),
            null, 1, "UW must approve");

        var failures = MeetingRosterEligibility.Check(
            Roster(("a", CommitteeMemberPosition.Chairman),
                   ("b", CommitteeMemberPosition.Secretary),
                   ("c", CommitteeMemberPosition.UW)),
            committee);

        failures.Should().BeEmpty();
    }

    [Fact]
    public void Check_UnresolvedSecretary_IsStillReported()
    {
        // The username check covers the whole roster, not just voters: a secretary who resolves to
        // nobody is a roster error worth surfacing before release.
        var committee = BuildCommittee(QuorumType.Fixed, 1);

        var failures = MeetingRosterEligibility.Check(
            Roster(("alice", CommitteeMemberPosition.Chairman),
                   ("ghost", CommitteeMemberPosition.Secretary)),
            committee,
            knownUsernames: Known("alice"));

        failures.Should().ContainSingle()
            .Which.Should().Be("no such user: ghost");
    }

    // -- Helpers --

    private static IReadOnlySet<string> Known(params string[] usernames)
        => usernames.ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static Committee BuildCommittee(QuorumType quorumType, int quorumValue)
        => Committee.Create("Committee With Meeting", MeetingCommittee.WithMeetingCode, null,
            quorumType, quorumValue, MajorityType.Unanimous, VotingMode.WaitForAll);

    private static List<MeetingMember> Roster(params (string UserId, CommitteeMemberPosition Position)[] members)
        => members
            .Select(m => MeetingMember.CreateManual(Guid.NewGuid(), m.UserId, m.UserId, m.Position))
            .ToList();
}
