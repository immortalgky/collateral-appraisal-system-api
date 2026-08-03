using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Workflow.AssigneeSelection.Core;
using Workflow.AssigneeSelection.Strategies;
using Workflow.AssigneeSelection.Teams;
using Xunit;

namespace Workflow.Tests.AssigneeSelection;

public class PoolAssigneeSelectorTests
{
    private readonly PoolAssigneeSelector _sut =
        new(Substitute.For<ILogger<PoolAssigneeSelector>>());

    private static AssignmentContext CreateContext(
        List<string>? userGroups = null,
        string? teamId = null,
        List<TeamMemberInfo>? candidatePool = null) => new()
    {
        WorkflowInstanceId = Guid.NewGuid(),
        ActivityName = "int-appraisal-verification",
        UserGroups = userGroups ?? ["IntAppraisalVerifier"],
        TeamId = teamId,
        CandidatePool = candidatePool
    };

    [Fact]
    public async Task SelectAssignee_NoResolvedTeam_EmitsBareGroup()
    {
        // Arrange — the pool holds the whole group (no team constraint, or none derivable), each
        // member carrying their own team. Nothing here identifies "the" team for the assignment.
        var context = CreateContext(
            teamId: null,
            candidatePool:
            [
                new TeamMemberInfo("praset", "Praset", "BBBB-2222", ["IntAppraisalVerifier"]),
                new TeamMemberInfo("somchai", "Somchai", "AAAA-1111", ["IntAppraisalVerifier"])
            ]);

        // Act
        var result = await _sut.SelectAssigneeAsync(context);

        // Assert — must NOT inherit the first candidate's team, which would hide the task from
        // every other team in the group.
        result.IsSuccess.Should().BeTrue();
        result.AssigneeId.Should().Be("IntAppraisalVerifier");
        result.Metadata!["AssignedType"].Should().Be("2");
    }

    [Fact]
    public async Task SelectAssignee_ResolvedTeam_ScopesGroupToThatTeam()
    {
        // Arrange — the pipeline genuinely resolved a team (team-constrained activity).
        var context = CreateContext(
            teamId: "AAAA-1111",
            candidatePool:
            [
                new TeamMemberInfo("somchai", "Somchai", "AAAA-1111", ["IntAppraisalVerifier"])
            ]);

        // Act
        var result = await _sut.SelectAssigneeAsync(context);

        // Assert — matches the "<group>:Team_<id>" shape PoolTaskAccess builds for callers.
        result.IsSuccess.Should().BeTrue();
        result.AssigneeId.Should().Be("IntAppraisalVerifier:Team_AAAA-1111");
    }

    [Fact]
    public async Task SelectAssignee_NoUserGroups_Fails()
    {
        // Arrange — a pool assignment with no group is a misconfiguration; cascade to the next strategy.
        var context = CreateContext(userGroups: []);

        // Act
        var result = await _sut.SelectAssigneeAsync(context);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("UserGroup");
    }
}
