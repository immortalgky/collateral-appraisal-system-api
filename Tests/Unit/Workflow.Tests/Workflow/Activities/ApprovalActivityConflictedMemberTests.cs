using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared.Data.Outbox;
using Shared.Time;
using Workflow.Data;
using Workflow.Data.Repository;
using Workflow.Domain.Committees;
using Workflow.Services.Configuration;
using Workflow.Services.Configuration.Models;
using Workflow.Sla.Services;
using Workflow.Tasks.Authorization;
using Workflow.Workflow.Activities;
using Workflow.Workflow.Activities.Approval;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Events;
using Workflow.Workflow.Models;
using Xunit;

namespace Workflow.Tests.Workflow.Activities;

/// <summary>
/// A Conflicted member, a committee member who completed one of the configured earlier activities of the
/// same appraisal (excludeAssigneesFrom on pending-approval), gets no approval task and cannot vote.
/// </summary>
public class ApprovalActivityConflictedMemberTests
{
    private readonly IApprovalMemberResolver _memberResolver = Substitute.For<IApprovalMemberResolver>();
    private readonly IApprovalVoteRepository _voteRepository = Substitute.For<IApprovalVoteRepository>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly ITaskConfigurationService _configurationService = Substitute.For<ITaskConfigurationService>();
    private readonly WorkflowDbContext _dbContext = new(
        new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase($"approval-conflicted-{Guid.NewGuid()}")
            .Options);
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();

    private const string ActivityId = "pending-approval";
    private const string Normalized = "pending_approval";

    private List<ApprovalMemberInfo> _committee =
    [
        new("presenter", nameof(CommitteeMemberPosition.Chairman)),
        new("member-2", nameof(CommitteeMemberPosition.Member)),
        new("member-3", nameof(CommitteeMemberPosition.Member))
    ];

    private QuorumConfig _quorum = new("Fixed", 1);
    private MajorityConfig _majority = new("Simple", "approve");
    private List<ApprovalConditionInfo> _conditions = [];
    private string _votingMode = "Quorum";

    public ApprovalActivityConflictedMemberTests()
    {
        _clock.ApplicationNow.Returns(DateTime.Now);

        _memberResolver.ResolveMembersAsync(
                Arg.Any<MemberSourceConfig>(), Arg.Any<Dictionary<string, object>>(),
                Arg.Any<QuorumConfig?>(), Arg.Any<MajorityConfig?>(), Arg.Any<CancellationToken>())
            .Returns(_ => new ApprovalGroupInfo(
                Members: _committee,
                Quorum: _quorum,
                Majority: _majority,
                Conditions: _conditions,
                CommitteeName: "Sub-committee",
                CommitteeCode: "SUB_COMMITTEE",
                VotingMode: _votingMode));
    }

    [Fact]
    public async Task Execute_MemberWhoCompletedAConfiguredActivity_GetsNoTask()
    {
        ConfigureExclusions("int-appraisal-execution", "int-appraisal-verification");
        var context = BuildContext(("int-appraisal-execution", "presenter"));

        var result = await BuildActivity().ExecuteAsync(context, CancellationToken.None);

        CapturedAssignment().Members.Select(m => m.Username).Should().BeEquivalentTo(["member-2", "member-3"]);
        ((List<ApprovalMemberInfo>)result.OutputData[$"{Normalized}_members"]).Select(m => m.Username)
            .Should().BeEquivalentTo(["member-2", "member-3"]);
        result.OutputData[$"{Normalized}_totalMembers"].Should().Be(2);
    }

    [Fact]
    public async Task Execute_MemberWhoCompletedTheActivityOnlyInAnEarlierRoutedBackRound_GetsNoTask()
    {
        ConfigureExclusions("int-appraisal-check");
        // Round 1 checked by member-2, routed back; round 2 checked by someone off the committee.
        var context = BuildContext(("int-appraisal-check", "member-2"), ("int-appraisal-check", "outsider"));

        await BuildActivity().ExecuteAsync(context, CancellationToken.None);

        CapturedAssignment().Members.Select(m => m.Username).Should().BeEquivalentTo(["presenter", "member-3"]);
    }

    [Fact]
    public async Task Execute_ComparesUserNamesCaseInsensitively()
    {
        _committee = [new("P2560", "Chairman"), new("member-2", "Member")];
        ConfigureExclusions("int-appraisal-verification");
        var context = BuildContext(("int-appraisal-verification", "p2560"));

        await BuildActivity().ExecuteAsync(context, CancellationToken.None);

        CapturedAssignment().Members.Select(m => m.Username).Should().BeEquivalentTo(["member-2"]);
    }

    [Fact]
    public async Task Execute_MemberWhoCompletedAnActivityThatIsNotConfigured_KeepsTheTask()
    {
        ConfigureExclusions("int-appraisal-verification");
        var context = BuildContext(("appraisal-assignment", "presenter"));

        await BuildActivity().ExecuteAsync(context, CancellationToken.None);

        CapturedAssignment().Members.Select(m => m.Username)
            .Should().BeEquivalentTo(["presenter", "member-2", "member-3"]);
    }

    [Fact]
    public async Task Execute_WithoutADbOverride_UsesTheWorkflowDefinitionList()
    {
        var context = BuildContext(("int-appraisal-execution", "presenter"));
        AttachDefinition(context, "int-appraisal-execution");

        await BuildActivity().ExecuteAsync(context, CancellationToken.None);

        CapturedAssignment().Members.Select(m => m.Username).Should().BeEquivalentTo(["member-2", "member-3"]);
    }

    [Fact]
    public async Task Execute_DbOverrideBeatsTheWorkflowDefinitionList()
    {
        ConfigureExclusions("int-appraisal-check");
        var context = BuildContext(("int-appraisal-execution", "presenter"), ("int-appraisal-check", "member-2"));
        AttachDefinition(context, "int-appraisal-execution");

        await BuildActivity().ExecuteAsync(context, CancellationToken.None);

        CapturedAssignment().Members.Select(m => m.Username).Should().BeEquivalentTo(["presenter", "member-3"]);
    }

    [Fact]
    public async Task Execute_EmptyDbOverride_ExcludesNobody()
    {
        ConfigureExclusions();
        var context = BuildContext(("int-appraisal-execution", "presenter"));
        AttachDefinition(context, "int-appraisal-execution");

        await BuildActivity().ExecuteAsync(context, CancellationToken.None);

        CapturedAssignment().Members.Select(m => m.Username)
            .Should().BeEquivalentTo(["presenter", "member-2", "member-3"]);
    }

    [Fact]
    public async Task Execute_MeetingRoster_IsFilteredTheSameWay()
    {
        ConfigureExclusions("int-appraisal-verification");
        var context = BuildContext(("int-appraisal-verification", "meeting-2"));
        context.Variables["meetingMemberOverrides"] = new List<ApprovalActivity.MeetingMemberOverride>
        {
            new("meeting-1", nameof(CommitteeMemberPosition.Chairman)),
            new("meeting-2", nameof(CommitteeMemberPosition.Member))
        };

        var result = await BuildActivity().ExecuteAsync(context, CancellationToken.None);

        CapturedAssignment().Members.Select(m => m.Username).Should().BeEquivalentTo(["meeting-1"]);
        result.OutputData[$"{Normalized}_totalMembers"].Should().Be(1);
    }

    [Fact]
    public async Task Execute_RecordsWhoWasExcludedAndBecauseOfWhichActivity()
    {
        ConfigureExclusions("int-appraisal-execution", "int-appraisal-check");
        var context = BuildContext(("int-appraisal-execution", "presenter"), ("int-appraisal-check", "member-2"));

        var result = await BuildActivity().ExecuteAsync(context, CancellationToken.None);

        result.OutputData[$"{Normalized}_excludedMembers"].Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["presenter"] = "int-appraisal-execution",
            ["member-2"] = "int-appraisal-check"
        });
    }

    [Fact]
    public async Task Execute_WithNoConflictedMember_RecordsAnEmptyExclusionList()
    {
        // A route-back re-opens the round; a stale list from the previous round must not survive it.
        ConfigureExclusions("int-appraisal-execution");

        var result = await BuildActivity().ExecuteAsync(BuildContext(), CancellationToken.None);

        ((IDictionary<string, string>)result.OutputData[$"{Normalized}_excludedMembers"]).Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_NoMembersLeft_RefusesTheRoundAndPublishesNothing()
    {
        _committee = [new("presenter", "Chairman")];
        ConfigureExclusions("int-appraisal-execution");

        await AssertRoundRefused(BuildContext(("int-appraisal-execution", "presenter")));
    }

    [Fact]
    public async Task Execute_FixedCountMajorityAboveTheMembersLeft_RefusesTheRound()
    {
        _majority = new MajorityConfig("FixedCount", "approve", 3);
        ConfigureExclusions("int-appraisal-execution");

        await AssertRoundRefused(BuildContext(("int-appraisal-execution", "presenter")));
    }

    [Fact]
    public async Task Execute_RequiredRoleHeldOnlyByAConflictedMember_RefusesTheRound()
    {
        _conditions = [new ApprovalConditionInfo("RoleRequired", "Chairman", null)];
        ConfigureExclusions("int-appraisal-execution");

        await AssertRoundRefused(BuildContext(("int-appraisal-execution", "presenter")));
    }

    [Fact]
    public async Task Execute_QuorumModeWithAFixedQuorumAboveTheMembersLeft_RefusesTheRound()
    {
        _quorum = new QuorumConfig("Fixed", 3);
        ConfigureExclusions("int-appraisal-execution");

        await AssertRoundRefused(BuildContext(("int-appraisal-execution", "presenter")));
    }

    [Fact]
    public async Task Execute_MinVotesAboveTheMembersLeft_RefusesTheRound()
    {
        _conditions = [new ApprovalConditionInfo("MinVotes", null, 3)];
        ConfigureExclusions("int-appraisal-execution");

        await AssertRoundRefused(BuildContext(("int-appraisal-execution", "presenter")));
    }

    [Fact]
    public async Task Execute_WaitForAllWithAFixedQuorumAboveTheMembersLeft_StillOpensTheRound()
    {
        // WaitForAll resolves once everyone has voted; the quorum number is never consulted.
        _votingMode = "WaitForAll";
        _quorum = new QuorumConfig("Fixed", 3);
        ConfigureExclusions("int-appraisal-execution");

        var result = await BuildActivity().ExecuteAsync(
            BuildContext(("int-appraisal-execution", "presenter")), CancellationToken.None);

        result.Status.Should().Be(ActivityResultStatus.Pending);
    }

    [Fact]
    public async Task Execute_RefusedRound_NamesTheCommitteeTheExcludedMembersAndTheRemainingCount()
    {
        _quorum = new QuorumConfig("Fixed", 3);
        ConfigureExclusions("int-appraisal-execution");

        var act = () => BuildActivity().ExecuteAsync(
            BuildContext(("int-appraisal-execution", "presenter")), CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<ApprovalRoundUnresolvableException>()).Which;
        ex.Message.Should().Contain("Sub-committee").And.Contain("presenter (int-appraisal-execution)")
            .And.Contain("2 member(s)").And.Contain("admin");
    }

    [Fact]
    public async Task Resume_VoteFromAnExcludedMember_IsRejected()
    {
        ConfigureExclusions("int-appraisal-execution");
        var context = BuildContext(("int-appraisal-execution", "presenter"));
        var activity = BuildActivity();
        var opened = await activity.ExecuteAsync(context, CancellationToken.None);
        foreach (var (key, value) in opened.OutputData)
        {
            context.WorkflowInstance.Variables[key] = value;
            context.Variables[key] = value;
        }

        var result = await activity.ResumeAsync(context, new Dictionary<string, object>
        {
            ["completedBy"] = "presenter",
            ["decisionTaken"] = "approve"
        }, CancellationToken.None);

        result.Status.Should().Be(ActivityResultStatus.Failed);
        result.ErrorMessage.Should().Contain("not a member");
        await _voteRepository.DidNotReceiveWithAnyArgs().AddVoteAsync(default!, default);
    }

    // -- Helpers --

    private async Task AssertRoundRefused(ActivityContext context)
    {
        var act = () => BuildActivity().ExecuteAsync(context, CancellationToken.None);

        // Not ActivityResult.Failed: the engine would turn that into a terminal WorkflowStatus.Failed.
        await act.Should().ThrowAsync<ApprovalRoundUnresolvableException>();
        _publisher.ReceivedCalls().Should().BeEmpty();
    }

    private ApprovalActivity BuildActivity() =>
        new(_memberResolver, _voteRepository, _publisher, Substitute.For<IIntegrationEventOutbox>(),
            Substitute.For<ICommitteeRepository>(), _dbContext, _clock, Substitute.For<ISlaCalculator>(),
            new SegregationOfDutiesGuard(_dbContext, _configurationService,
                Substitute.For<ILogger<SegregationOfDutiesGuard>>()),
            Substitute.For<ILogger<ApprovalActivity>>());

    private void ConfigureExclusions(params string[] activityIds) =>
        _configurationService.GetConfigurationAsync(ActivityId, Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(new TaskAssignmentConfigurationDto { ActivityId = ActivityId, ExcludeAssigneesFrom = [.. activityIds] });

    /// <summary>An instance whose history holds the given activities, each completed by the given user.</summary>
    private static ActivityContext BuildContext(params (string ActivityId, string CompletedBy)[] history)
    {
        var instance = WorkflowInstance.Create(Guid.NewGuid(), "Test Workflow", null, "system");
        instance.UpdateVariables(new Dictionary<string, object>
        {
            ["appraisalId"] = Guid.NewGuid(),
            ["appraisalValue"] = 5_000_000m
        });

        foreach (var (activityId, completedBy) in history)
        {
            var execution = WorkflowActivityExecution.Create(instance.Id, activityId, activityId, "TaskActivity");
            execution.Start();
            execution.Complete(completedBy);
            instance.AddActivityExecution(execution);
        }

        return new ActivityContext
        {
            WorkflowInstanceId = instance.Id,
            ActivityId = ActivityId,
            ActivityName = "Committee Approval",
            WorkflowInstance = instance,
            Variables = new Dictionary<string, object>(instance.Variables),
            Properties = new Dictionary<string, object>
            {
                ["memberSource"] = new MemberSourceConfig("committee", null, "SUB_COMMITTEE", null, null, null),
                ["activityName"] = "PendingApproval"
            }
        };
    }

    /// <summary>Puts <c>assignmentRules.excludeAssigneesFrom</c> on pending-approval in the workflow definition JSON.</summary>
    private static void AttachDefinition(ActivityContext context, params string[] excludeAssigneesFrom)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            activities = new[]
            {
                new { id = ActivityId, assignmentRules = new { excludeAssigneesFrom } }
            }
        });
        typeof(WorkflowInstance).GetProperty(nameof(WorkflowInstance.WorkflowDefinition))!
            .SetValue(context.WorkflowInstance, WorkflowDefinition.Create("wf", "wf", json, "test", "tester"));
    }

    private ApprovalTasksAssignedEvent CapturedAssignment() =>
        _publisher.ReceivedCalls()
            .Select(c => c.GetArguments()[0])
            .OfType<ApprovalTasksAssignedEvent>()
            .Single();
}
