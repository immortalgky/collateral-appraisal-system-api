using FluentAssertions;
using Workflow.Domain.Committees;
using Xunit;

namespace Workflow.Tests.Domain;

/// <summary>
/// A RoleRequired condition is satisfied by a plain case-insensitive string comparison against the
/// role stamped on each vote (ApprovalActivity.CheckApprovalConditions), and that role is a
/// CommitteeMemberPosition name. A RoleRequired value outside the enum therefore matches no vote
/// ever cast: the round's conditionsMet stays false, it never resolves, and nothing surfaces an
/// error. These tests pin the guard that stops such a value being stored in the first place.
/// </summary>
public class CommitteeAddConditionTests
{
    [Theory]
    [InlineData("UW")]
    [InlineData("uw")]
    [InlineData("Chairman")]
    [InlineData("Director")]
    public void AddCondition_RoleRequired_AcceptsAPositionName(string role)
    {
        var committee = BuildCommittee();

        var condition = committee.AddCondition(
            ConditionType.RoleRequired, role, minVotesRequired: null, priority: 1, description: null);

        condition.RoleRequired.Should().Be(role);
        committee.Conditions.Should().ContainSingle();
    }

    [Theory]
    [InlineData("COMMITTEE")]          // a committee CODE, not a position — the original bug
    [InlineData("COMMITTEE_WITH_MEETING")]
    [InlineData("Underwriter")]        // plausible synonym for UW
    [InlineData("")]
    [InlineData(null)]
    public void AddCondition_RoleRequired_RejectsAnythingThatIsNotAPositionName(string? role)
    {
        var committee = BuildCommittee();

        var act = () => committee.AddCondition(
            ConditionType.RoleRequired, role, minVotesRequired: null, priority: 1, description: null);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Allowed values*");
        committee.Conditions.Should().BeEmpty();
    }

    [Fact]
    public void AllowedValueMessages_AreDeterministicAndInDisplayOrder()
    {
        // Built from an ordered array, not from the HashSet: a set's iteration order is not
        // guaranteed, so the text a client sees could otherwise vary between runs.
        CommitteeMemberPositions.SelectableNames
            .Should().Be("Chairman, Director, Secretary, UW");

        CommitteeMemberPositions.RequirableNames
            .Should().Be("Chairman, Director, UW", "the secretary never votes");
    }

    [Fact]
    public void AddCondition_RoleRequired_InvalidNameMessageOnlyListsRolesItWouldAccept()
    {
        // The message used to advertise the selectable set, which includes the Secretary — so an
        // admin could copy a value straight out of the error and be refused again by the next guard.
        var committee = BuildCommittee();

        var act = () => committee.AddCondition(
            ConditionType.RoleRequired, "Cheirman", minVotesRequired: null, priority: 1, description: null);

        var message = act.Should().Throw<ArgumentException>().Which.Message;

        message.Should().NotContain(nameof(CommitteeMemberPosition.Secretary));
        foreach (var retired in Enum.GetValues<CommitteeMemberPosition>()
                     .Where(p => !CommitteeMemberPositions.Selectable.Contains(p)))
            message.Should().NotContain(retired.ToString());

        message.Should().Contain(nameof(CommitteeMemberPosition.Chairman));
        message.Should().Contain(nameof(CommitteeMemberPosition.UW));
    }

    [Fact]
    public void AddCondition_RoleRequired_RejectsTheNumericFormOfAPosition()
    {
        // Enum.TryParse would accept "3" and map it to UW, but RoleRequired is persisted as the raw
        // string — so "3" would be stored and then match no vote's role at runtime.
        var committee = BuildCommittee();

        var act = () => committee.AddCondition(
            ConditionType.RoleRequired, "3", minVotesRequired: null, priority: 1, description: null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddCondition_MinVotes_IsUnaffectedByTheRoleGuard()
    {
        var committee = BuildCommittee();

        var condition = committee.AddCondition(
            ConditionType.MinVotes, roleRequired: null, minVotesRequired: 3, priority: 1, description: null);

        condition.MinVotesRequired.Should().Be(3);
    }

    [Fact]
    public void AddCondition_RoleRequired_AcceptsEverySelectableVotingPosition()
    {
        // The guard must not drift from the positions a vote role can actually be produced from:
        // currently selectable, and able to vote. The Secretary is selectable but never votes.
        var expected = CommitteeMemberPositions.Selectable
            .Where(CommitteeMemberPositions.CanVote)
            .ToList();

        foreach (var position in expected)
        {
            var committee = BuildCommittee();
            var act = () => committee.AddCondition(
                ConditionType.RoleRequired, position.ToString(), null, 1, null);

            act.Should().NotThrow($"{position} is selectable and votes");
        }
    }

    [Fact]
    public void AddCondition_RoleRequired_RejectsSecretary_BecauseTheyNeverVote()
    {
        // Meeting.ReleaseItem drops the Secretary from the approver roster, so no vote can ever
        // carry that role — requiring it would open a round that can never satisfy the condition.
        var committee = BuildCommittee();

        var act = () => committee.AddCondition(
            ConditionType.RoleRequired, nameof(CommitteeMemberPosition.Secretary), null, 1, null);

        act.Should().Throw<ArgumentException>().WithMessage("*does not vote*");
    }

    [Theory]
    [InlineData("Risk")]
    [InlineData("Appraisal")]
    [InlineData("Credit")]
    [InlineData("Member")]
    public void AddCondition_RoleRequired_RejectsRetiredPositions(string role)
    {
        // Still on the enum so existing rows materialize, but no longer assignable — a condition
        // requiring one could only be satisfied by a member nobody can create any more.
        var committee = BuildCommittee();

        var act = () => committee.AddCondition(
            ConditionType.RoleRequired, role, null, 1, null);

        act.Should().Throw<ArgumentException>().WithMessage("*retired*");
    }

    private static Committee BuildCommittee()
        => Committee.Create("Committee", "COMMITTEE", null,
            QuorumType.Fixed, 3, MajorityType.Simple, VotingMode.WaitForAll);
}
