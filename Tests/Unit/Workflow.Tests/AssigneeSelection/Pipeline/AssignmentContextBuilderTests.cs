using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared.Time;
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

    // ── Fan-out stage Tier 2 tests ────────────────────────────────────────────

    [Fact]
    public async Task BuildAsync_FanOutStageTransition_DerivesTeamFromMakerCompletedBy()
    {
        // Arrange — alice@companyA completed the maker stage; checker stage is being assigned.
        // The execution is still InProgress (fan-out stage transition), so
        // GetMostRecentPriorAssignee would return null. The fan-out branch must find alice.
        const string makerUser = "alice@companyA";
        var companyId = Guid.NewGuid();
        var teamId = Guid.NewGuid().ToString();

        _teamService.GetTeamForUserAsync(makerUser, Arg.Any<CancellationToken>())
            .Returns(new TeamInfo(teamId, "Company A Team", TeamType.External, true));

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(DateTime.UtcNow);

        var context = CreateFanOutPipelineContext(
            fanOutKey: companyId,
            makerCompletedBy: makerUser,
            dateTimeProvider: dateTimeProvider,
            properties: new Dictionary<string, object>
            {
                ["assignmentRules"] = new Dictionary<string, object> { ["teamConstrained"] = true }
            });

        // Act
        await _sut.BuildAsync(context);

        // Assert — team is derived from the maker's CompletedBy, not from a Completed execution
        context.TeamId.Should().Be(teamId);
        await _teamService.Received(1).GetTeamForUserAsync(makerUser, Arg.Any<CancellationToken>());

        // Assert — PriorAssignees map keys the maker's stage by the actor's CompletedBy
        // (not AssigneeUserId). This guards against regressing BuildPriorAssigneesMap back
        // to reading AssigneeUserId, which is null on the maker spawn.
        var stageKey = $"{context.ActivityContext.ActivityId}:maker";
        context.PriorAssignees.Should().ContainKey(stageKey);
        context.PriorAssignees[stageKey].Should().Be(makerUser);
    }

    [Fact]
    public async Task BuildAsync_FanOutStageTransition_FallsThroughToCrossActivityPathWhenNoCompletedBy()
    {
        // Arrange — there is no CompletedBy on the stage history yet (first stage of a new item
        // that hasn't been completed yet). The cross-activity path should handle it instead.
        var companyId = Guid.NewGuid();
        var priorAssigneeId = Guid.NewGuid().ToString();
        var teamId = Guid.NewGuid().ToString();

        _teamService.GetTeamForUserAsync(priorAssigneeId, Arg.Any<CancellationToken>())
            .Returns(new TeamInfo(teamId, "Prior Team", TeamType.Internal, true));

        var context = CreatePipelineContext(
            properties: new Dictionary<string, object>
            {
                ["assignmentRules"] = new Dictionary<string, object> { ["teamConstrained"] = true }
            },
            priorAssigneeId: priorAssigneeId,
            fanOutKey: companyId,
            hasStageHistory: false);

        // Act
        await _sut.BuildAsync(context);

        // Assert — fan-out branch found nothing (no CompletedBy), fell through to cross-activity
        context.TeamId.Should().Be(teamId);
        await _teamService.Received(1).GetTeamForUserAsync(priorAssigneeId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BuildAsync_FanOutStageTransition_NoFanOutKeyFallsBackToGlobalTeamId()
    {
        // Arrange — teamConstrained=true, no FanOutKey in ActivityContext, no prior completed execution.
        // Falls through all Tier 2 paths to Tier 3 (global TeamId variable).
        var globalTeamId = Guid.NewGuid().ToString();
        var context = CreatePipelineContext(
            properties: new Dictionary<string, object>
            {
                ["assignmentRules"] = new Dictionary<string, object> { ["teamConstrained"] = true }
            },
            variables: new Dictionary<string, object>
            {
                ["TeamId"] = globalTeamId
            });

        // Act
        await _sut.BuildAsync(context);

        // Assert — no team derivation possible; Tier 3 global variable used
        context.TeamId.Should().Be(globalTeamId);
        await _teamService.DidNotReceive().GetTeamForUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // --- Helpers ---

    private static AssignmentPipelineContext CreatePipelineContext(
        Dictionary<string, object>? properties = null,
        Dictionary<string, object>? variables = null,
        string? priorAssigneeId = null,
        Guid? fanOutKey = null,
        bool hasStageHistory = false)
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

        // Add an in-progress fan-out execution with empty stage history when requested
        if (fanOutKey.HasValue && !hasStageHistory)
        {
            var dateTimeProvider = Substitute.For<IDateTimeProvider>();
            dateTimeProvider.ApplicationNow.Returns(DateTime.UtcNow);

            var fanOutExecution = WorkflowActivityExecution.Create(
                workflowInstance.Id,
                "int-appraisal-checker",
                "Ext Appraisal Assignment",
                "FanOutTaskActivity");
            fanOutExecution.Start();
            fanOutExecution.InitializeFanOutItem(fanOutKey.Value, "maker", "ExtAdmin", null, dateTimeProvider);
            // Note: NOT calling AdvanceStage — maker stage still open (no CompletedBy)
            workflowInstance.ActivityExecutions.Add(fanOutExecution);
        }

        var activityCtx = new ActivityContext
        {
            WorkflowInstanceId = workflowInstance.Id,
            ActivityId = "int-appraisal-checker",
            Properties = properties ?? new Dictionary<string, object>(),
            Variables = variables ?? new Dictionary<string, object>(),
            WorkflowInstance = workflowInstance,
            FanOutKey = fanOutKey
        };

        return new AssignmentPipelineContext { ActivityContext = activityCtx };
    }

    /// <summary>
    /// Creates a pipeline context simulating a fan-out stage transition:
    /// an in-flight execution with a completed maker stage (<paramref name="makerCompletedBy"/>
    /// stamped) and the activity context set to the same activity with the fan-out key.
    /// </summary>
    private static AssignmentPipelineContext CreateFanOutPipelineContext(
        Guid fanOutKey,
        string makerCompletedBy,
        IDateTimeProvider dateTimeProvider,
        Dictionary<string, object>? properties = null)
    {
        var workflowInstance = WorkflowInstance.Create(
            Guid.NewGuid(),
            "test-workflow",
            null,
            "test-user");

        // Create an InProgress fan-out execution for the same activityId that will be in the context
        const string activityId = "ext-appraisal-assignment";
        var execution = WorkflowActivityExecution.Create(
            workflowInstance.Id,
            activityId,
            "Ext Appraisal Assignment",
            "FanOutTaskActivity");
        execution.Start();

        // Maker stage: initialized as the production spawn does (assigneeUserId=null because
        // the maker is assigned to a group pool, not a specific user), then advanced — which
        // closes the maker entry stamping ExitedOn + CompletedBy.
        execution.InitializeFanOutItem(fanOutKey, "maker", "ExtMaker", assigneeUserId: null, dateTimeProvider);
        execution.AdvanceStage(fanOutKey, "checker", "ExtChecker", null, dateTimeProvider, makerCompletedBy);

        workflowInstance.ActivityExecutions.Add(execution);

        var activityCtx = new ActivityContext
        {
            WorkflowInstanceId = workflowInstance.Id,
            ActivityId = activityId,
            Properties = properties ?? new Dictionary<string, object>(),
            Variables = new Dictionary<string, object>(),
            WorkflowInstance = workflowInstance,
            FanOutKey = fanOutKey
        };

        return new AssignmentPipelineContext { ActivityContext = activityCtx };
    }
}
