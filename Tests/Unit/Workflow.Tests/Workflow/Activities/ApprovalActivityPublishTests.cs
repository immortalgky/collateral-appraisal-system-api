using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Shared.Time;
using Workflow.Data.Repository;
using Workflow.Domain;
using Workflow.Domain.Committees;
using Workflow.Workflow.Activities;
using Workflow.Workflow.Activities.Approval;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;
using Xunit;

namespace Workflow.Tests.Workflow.Activities;

/// <summary>
/// Verifies that ApprovalActivity populates AppraisalNo and CommitteeId on
/// AppraisalApprovedIntegrationEvent when decision == "approve".
/// </summary>
public class ApprovalActivityPublishTests
{
    private readonly IApprovalMemberResolver _memberResolver = Substitute.For<IApprovalMemberResolver>();
    private readonly IApprovalVoteRepository _voteRepository = Substitute.For<IApprovalVoteRepository>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly IIntegrationEventOutbox _outbox = Substitute.For<IIntegrationEventOutbox>();
    private readonly ICommitteeRepository _committeeRepository = Substitute.For<ICommitteeRepository>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();

    private ApprovalActivity BuildActivity() =>
        new(_memberResolver, _voteRepository, _publisher, _outbox, _committeeRepository,
            _clock, Substitute.For<ILogger<ApprovalActivity>>());

    [Fact]
    public async Task ResumeActivityAsync_WhenDecisionIsApprove_PublishesEventWithAppraisalNoAndCommitteeId()
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        var activity = BuildActivity();

        var appraisalId = Guid.NewGuid();
        var expectedCommitteeId = Guid.NewGuid();
        const string committeeCode = "SUB";
        const string appraisalNo = "2568-0001";

        // Set up committee repository to return a committee with the expected Id
        var committee = Committee.Create("Sub-Committee", committeeCode, null,
            QuorumType.Fixed, 1, MajorityType.Simple);
        // Set committee Id via reflection (Committee.Id is set in Create via Guid.CreateVersion7)
        // We work with the actual committee returned — record its Id for assertions
        _committeeRepository.GetByCodeAsync(committeeCode, Arg.Any<CancellationToken>())
            .Returns(committee);

        _clock.ApplicationNow.Returns(DateTime.UtcNow);

        var activityId = "approval-1";
        var normalizedId = activityId.Replace("-", "_").Replace(" ", "_").Replace(".", "_").ToLowerInvariant();

        // Build workflow instance with the required variables
        var instance = WorkflowInstance.Create(Guid.NewGuid(), "Test Workflow", null, "system");
        instance.UpdateVariables(new Dictionary<string, object>
        {
            ["appraisalId"] = appraisalId,
            ["appraisalNumber"] = appraisalNo,
            [$"{normalizedId}_members"] = new List<ApprovalMemberInfo>
            {
                new("voter1", "Analyst")
            },
            [$"{normalizedId}_quorum"] = new QuorumConfig("Fixed", 1),
            [$"{normalizedId}_majority"] = new MajorityConfig("Simple", "approve"),
            [$"{normalizedId}_conditions"] = new List<ApprovalConditionInfo>(),
            [$"{normalizedId}_voteOptions"] = new List<string> { "approve", "reject" },
            [$"{normalizedId}_committeeName"] = "Sub-Committee",
            [$"{normalizedId}_committeeCode"] = committeeCode,
            [$"{normalizedId}_totalMembers"] = 1,
            [$"{normalizedId}_votesReceived"] = 0,
            ["activityName"] = activityId
        });

        // Add an InProgress execution so FindActivityExecution succeeds
        var execution = WorkflowActivityExecution.Create(
            instance.Id, activityId, "Approval Activity", ActivityTypes.ApprovalActivity);
        execution.Start();
        instance.AddActivityExecution(execution);

        // Voter has not yet voted
        _voteRepository.HasMemberVotedAsync(execution.Id, "voter1", Arg.Any<CancellationToken>())
            .Returns(false);

        // Returning the one "approve" vote (quorum=1 so this is the deciding vote)
        var approveVote = ApprovalVote.Create(instance.Id, activityId, execution.Id, "voter1", "Analyst", "approve", null);
        _voteRepository.GetVotesForExecutionAsync(execution.Id, Arg.Any<CancellationToken>())
            .Returns(new List<ApprovalVote> { approveVote });

        var context = new ActivityContext
        {
            WorkflowInstanceId = instance.Id,
            ActivityId = activityId,
            ActivityName = "Approval Activity",
            WorkflowInstance = instance,
            Variables = new Dictionary<string, object>(instance.Variables),
            Properties = new Dictionary<string, object>()
        };

        var resumeInput = new Dictionary<string, object>
        {
            ["completedBy"] = "voter1",
            ["decisionTaken"] = "approve"
        };

        // ── Act ───────────────────────────────────────────────────────────────
        // Call via the public ResumeAsync wrapper on the base class
        var result = await activity.ResumeAsync(context, resumeInput, CancellationToken.None);

        // ── Assert ────────────────────────────────────────────────────────────
        result.Status.Should().Be(ActivityResultStatus.Completed);

        _outbox.Received(1).Publish(
            Arg.Is<AppraisalApprovedIntegrationEvent>(e =>
                e.AppraisalId == appraisalId &&
                e.CommitteeCode == committeeCode &&
                e.AppraisalNo == appraisalNo &&
                e.CommitteeId == committee.Id),
            Arg.Any<string>());
    }

    [Fact]
    public async Task ResumeActivityAsync_WhenDecisionIsApprove_CommitteeNotFound_PublishesWithEmptyCommitteeId()
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        var activity = BuildActivity();

        var appraisalId = Guid.NewGuid();
        const string committeeCode = "UNKNOWN";

        // Committee repo returns null (committee not found)
        _committeeRepository.GetByCodeAsync(committeeCode, Arg.Any<CancellationToken>())
            .Returns((Committee?)null);

        _clock.ApplicationNow.Returns(DateTime.UtcNow);

        var activityId = "approval-1";
        var normalizedId = activityId.Replace("-", "_").Replace(" ", "_").Replace(".", "_").ToLowerInvariant();

        var instance = WorkflowInstance.Create(Guid.NewGuid(), "Test Workflow", null, "system");
        instance.UpdateVariables(new Dictionary<string, object>
        {
            ["appraisalId"] = appraisalId,
            [$"{normalizedId}_members"] = new List<ApprovalMemberInfo> { new("voter1", null) },
            [$"{normalizedId}_quorum"] = new QuorumConfig("Fixed", 1),
            [$"{normalizedId}_majority"] = new MajorityConfig("Simple", "approve"),
            [$"{normalizedId}_conditions"] = new List<ApprovalConditionInfo>(),
            [$"{normalizedId}_voteOptions"] = new List<string> { "approve", "reject" },
            [$"{normalizedId}_committeeName"] = "",
            [$"{normalizedId}_committeeCode"] = committeeCode,
            [$"{normalizedId}_totalMembers"] = 1,
            [$"{normalizedId}_votesReceived"] = 0,
            ["activityName"] = activityId
        });

        var execution = WorkflowActivityExecution.Create(
            instance.Id, activityId, "Approval Activity", ActivityTypes.ApprovalActivity);
        execution.Start();
        instance.AddActivityExecution(execution);

        _voteRepository.HasMemberVotedAsync(execution.Id, "voter1", Arg.Any<CancellationToken>())
            .Returns(false);

        var approveVote = ApprovalVote.Create(instance.Id, activityId, execution.Id, "voter1", null, "approve", null);
        _voteRepository.GetVotesForExecutionAsync(execution.Id, Arg.Any<CancellationToken>())
            .Returns(new List<ApprovalVote> { approveVote });

        var context = new ActivityContext
        {
            WorkflowInstanceId = instance.Id,
            ActivityId = activityId,
            ActivityName = "Approval Activity",
            WorkflowInstance = instance,
            Variables = new Dictionary<string, object>(instance.Variables),
            Properties = new Dictionary<string, object>()
        };

        // ── Act ───────────────────────────────────────────────────────────────
        var result = await activity.ResumeAsync(context,
            new Dictionary<string, object> { ["completedBy"] = "voter1", ["decisionTaken"] = "approve" },
            CancellationToken.None);

        // ── Assert ────────────────────────────────────────────────────────────
        result.Status.Should().Be(ActivityResultStatus.Completed);

        // Event is still published; CommitteeId falls back to Guid.Empty
        _outbox.Received(1).Publish(
            Arg.Is<AppraisalApprovedIntegrationEvent>(e =>
                e.AppraisalId == appraisalId &&
                e.CommitteeId == Guid.Empty),
            Arg.Any<string>());
    }
}
