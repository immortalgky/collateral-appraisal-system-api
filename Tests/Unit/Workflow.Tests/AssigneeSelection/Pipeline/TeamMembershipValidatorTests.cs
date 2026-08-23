using Workflow.AssigneeSelection.Pipeline;
using Workflow.AssigneeSelection.Teams;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Workflow.Tests.AssigneeSelection.Pipeline;

public class TeamMembershipValidatorTests
{
    private readonly ITeamService _teamService;
    private readonly TeamMembershipValidator _validator;

    public TeamMembershipValidatorTests()
    {
        _teamService = Substitute.For<ITeamService>();
        _validator = new TeamMembershipValidator(_teamService, Substitute.For<ILogger<TeamMembershipValidator>>());
    }

    private static AssignmentPipelineContext CreateContext(
        string teamId, string selectedAssignee, string selectionStrategy) => new()
    {
        ActivityContext = new ActivityContext(),
        Rules = new ActivityAssignmentRules(TeamConstrained: true, ExcludeAssigneesFrom: []),
        TeamId = teamId,
        SelectedAssignee = selectedAssignee,
        SelectionStrategy = selectionStrategy
    };

    [Fact]
    public async Task ValidateAsync_WrongTeamStrategyMismatch_ReturnsInvalid()
    {
        _teamService.GetTeamForUserAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(new TeamInfo("team-B", "Team B", TeamType.Internal, true));

        var context = CreateContext("team-A", "user-1", "round_robin");

        var result = await _validator.ValidateAsync(context);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_PreviousOwnerInDifferentTeam_ReturnsValid()
    {
        // The whole point of route-back: the historically-verified previous owner may legitimately
        // sit in a different team than the activity's current constraint.
        _teamService.GetTeamForUserAsync("verifier-1", Arg.Any<CancellationToken>())
            .Returns(new TeamInfo("team-B", "Team B", TeamType.Internal, true));

        var context = CreateContext("team-A", "verifier-1", "previous_owner");

        var result = await _validator.ValidateAsync(context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_PreviousOwnerNoLongerOnAnyTeam_ReturnsInvalid()
    {
        // The team-equality check is skipped for previous_owner, but the assignee must still resolve
        // to a real, currently active team member — this rejects a deleted/deactivated account (or an
        // accidental "SYSTEM" completion sentinel, which is never a registered team member).
        _teamService.GetTeamForUserAsync("deleted-user", Arg.Any<CancellationToken>())
            .Returns((TeamInfo?)null);

        var context = CreateContext("team-A", "deleted-user", "previous_owner");

        var result = await _validator.ValidateAsync(context);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainMatch("*does not belong to any team*");
    }

    [Fact]
    public async Task ValidateAsync_PoolAssigneeWrongTeamSuffix_ReturnsInvalid()
    {
        var context = CreateContext("team-A", "ExtAdmin:Team_team-B", "pool");

        var result = await _validator.ValidateAsync(context);

        result.IsValid.Should().BeFalse();
        await _teamService.DidNotReceive().GetTeamForUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateAsync_PoolAssigneeMatchingTeamSuffix_ReturnsValid()
    {
        var context = CreateContext("team-A", "ExtAdmin:Team_team-A", "pool");

        var result = await _validator.ValidateAsync(context);

        result.IsValid.Should().BeTrue();
    }
}
