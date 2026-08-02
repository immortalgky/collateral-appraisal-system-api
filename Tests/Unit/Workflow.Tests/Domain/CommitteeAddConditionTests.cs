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
    [InlineData("Member")]
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
    public void AddCondition_RoleRequired_AcceptsEveryDefinedPosition()
    {
        // The guard must not drift from the enum the vote role is produced from.
        foreach (var name in Enum.GetNames<CommitteeMemberPosition>())
        {
            var committee = BuildCommittee();
            var act = () => committee.AddCondition(
                ConditionType.RoleRequired, name, null, 1, null);

            act.Should().NotThrow($"{name} is a valid CommitteeMemberPosition");
        }
    }

    private static Committee BuildCommittee()
        => Committee.Create("Committee", "COMMITTEE", null,
            QuorumType.Fixed, 3, MajorityType.Simple, VotingMode.WaitForAll);
}
