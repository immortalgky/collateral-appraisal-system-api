using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Workflow.AssigneeSelection.Pipeline;
using Workflow.AssigneeSelection.Teams;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Xunit;

namespace Workflow.Tests.AssigneeSelection.Pipeline;

public class AssignmentContextBuilderTests
{
    private readonly ITeamService _teamService = Substitute.For<ITeamService>();
    private readonly ILogger<AssignmentContextBuilder> _logger = Substitute.For<ILogger<AssignmentContextBuilder>>();
    private readonly AssignmentContextBuilder _sut;

    public AssignmentContextBuilderTests()
    {
        _sut = new AssignmentContextBuilder(_teamService, _logger);
    }

    [Fact]
    public async Task BuildAsync_TeamConstrainedWithTeamIdVariable_UsesExplicitVariable()
    {
        // Arrange — external path: teamIdVariable points to assignedCompanyId
        var companyId = Guid.NewGuid().ToString();
        var context = CreatePipelineContext(
            properties: new Dictionary<string, object>
            {
                ["teamIdVariable"] = "assignedCompanyId",
                ["assignmentRules"] = new Dictionary<string, object> { ["teamConstrained"] = true }
            },
            variables: new Dictionary<string, object>
            {
                ["assignedCompanyId"] = companyId
            });

        // Act
        await _sut.BuildAsync(context);

        // Assert — should use the explicit variable, not call ITeamService
        context.TeamId.Should().Be(companyId);
        await _teamService.DidNotReceive().GetTeamForUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BuildAsync_TeamConstrainedNoVariable_LooksPreviousAssigneeTeam()
    {
        // Arrange — internal path: teamConstrained=true, no teamIdVariable, prior execution exists
        var priorAssigneeId = Guid.NewGuid().ToString();
        var teamId = Guid.NewGuid().ToString();

        _teamService.GetTeamForUserAsync(priorAssigneeId, Arg.Any<CancellationToken>())
            .Returns(new TeamInfo(teamId, "Team Alpha", TeamType.Internal, true));

        var context = CreatePipelineContext(
            properties: new Dictionary<string, object>
            {
                ["assignmentRules"] = new Dictionary<string, object> { ["teamConstrained"] = true }
            },
            priorAssigneeId: priorAssigneeId);

        // Act
        await _sut.BuildAsync(context);

        // Assert
        context.TeamId.Should().Be(teamId);
        context.Rules.TeamConstrained.Should().BeTrue();
        await _teamService.Received(1).GetTeamForUserAsync(priorAssigneeId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BuildAsync_TeamConstrainedNoPriorAssignee_TeamIdRemainsNull()
    {
        // Arrange — first assignment, no prior executions
        var context = CreatePipelineContext(
            properties: new Dictionary<string, object>
            {
                ["assignmentRules"] = new Dictionary<string, object> { ["teamConstrained"] = true }
            });

        // Act
        await _sut.BuildAsync(context);

        // Assert — no prior assignee, so TeamId stays null
        context.TeamId.Should().BeNull();
        await _teamService.DidNotReceive().GetTeamForUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BuildAsync_TeamConstrainedPriorAssigneeNoTeam_TeamIdRemainsNull()
    {
        // Arrange — prior assignee exists but has no team
        var priorAssigneeId = Guid.NewGuid().ToString();

        _teamService.GetTeamForUserAsync(priorAssigneeId, Arg.Any<CancellationToken>())
            .Returns((TeamInfo?)null);

        var context = CreatePipelineContext(
            properties: new Dictionary<string, object>
            {
                ["assignmentRules"] = new Dictionary<string, object> { ["teamConstrained"] = true }
            },
            priorAssigneeId: priorAssigneeId);

        // Act
        await _sut.BuildAsync(context);

        // Assert
        context.TeamId.Should().BeNull();
        await _teamService.Received(1).GetTeamForUserAsync(priorAssigneeId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BuildAsync_NotTeamConstrained_SkipsTeamLookup()
    {
        // Arrange — teamConstrained=false
        var priorAssigneeId = Guid.NewGuid().ToString();
        var context = CreatePipelineContext(
            priorAssigneeId: priorAssigneeId);

        // Act
        await _sut.BuildAsync(context);

        // Assert — should never call GetTeamForUserAsync
        context.Rules.TeamConstrained.Should().BeFalse();
        await _teamService.DidNotReceive().GetTeamForUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // --- Helpers ---

    private static AssignmentPipelineContext CreatePipelineContext(
        Dictionary<string, object>? properties = null,
        Dictionary<string, object>? variables = null,
        string? priorAssigneeId = null)
    {
        var workflowInstance = WorkflowInstance.Create(
            Guid.NewGuid(),
            "test-workflow",
            null,
            "test-user");

        // Add a completed execution if prior assignee is specified
        if (priorAssigneeId is not null)
        {
            var execution = WorkflowActivityExecution.Create(
                workflowInstance.Id,
                "int-appraisal-staff",
                "Internal Appraisal Staff",
                "TaskActivity",
                assignedTo: priorAssigneeId);

            execution.Complete(priorAssigneeId);
            workflowInstance.ActivityExecutions.Add(execution);
        }

        var activityCtx = new ActivityContext
        {
            WorkflowInstanceId = workflowInstance.Id,
            ActivityId = "int-appraisal-checker",
            Properties = properties ?? new Dictionary<string, object>(),
            Variables = variables ?? new Dictionary<string, object>(),
            WorkflowInstance = workflowInstance
        };

        return new AssignmentPipelineContext { ActivityContext = activityCtx };
    }
}
