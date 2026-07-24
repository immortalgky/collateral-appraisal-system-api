using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Shared.Time;
using Workflow.Data;
using Workflow.Data.Repository;
using Workflow.Domain;
using Workflow.Domain.Committees;
using Workflow.Workflow.Activities;
using Workflow.Workflow.Activities.Approval;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Events;
using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;
using Xunit;

namespace Workflow.Tests.Workflow.Activities;

/// <summary>
/// Verifies the two committee voting modes in <see cref="ApprovalActivity"/>:
///   WaitForAll — every member must vote, then the approve rule decides (not-approved = do nothing).
///   Quorum     — resolves as soon as quorum + majority met, closing the unvoted members' tasks.
/// Also covers the majority denominator (counted against ALL members, not votes cast).
/// </summary>
public class ApprovalActivityVotingModeTests
{
    private readonly IApprovalMemberResolver _memberResolver = Substitute.For<IApprovalMemberResolver>();
    private readonly IApprovalVoteRepository _voteRepository = Substitute.For<IApprovalVoteRepository>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly IIntegrationEventOutbox _outbox = Substitute.For<IIntegrationEventOutbox>();
    private readonly ICommitteeRepository _committeeRepository = Substitute.For<ICommitteeRepository>();
    private readonly WorkflowDbContext _dbContext = new(
        new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase($"approval-activity-voting-mode-{Guid.NewGuid()}")
            .Options);
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();

    private const string ActivityId = "approval-1";
    private const string CommitteeCode = "COMMITTEE";

    private ApprovalActivity BuildActivity() =>
        new(_memberResolver, _voteRepository, _publisher, _outbox, _committeeRepository,
            _dbContext, _clock, Substitute.For<global::Workflow.Sla.Services.ISlaCalculator>(),
            Substitute.For<ILogger<ApprovalActivity>>());

    private static string Normalize(string id) =>
        id.Replace("-", "_").Replace(" ", "_").Replace(".", "_").ToLowerInvariant();

    /// <summary>
    /// Drives a single resume call. <paramref name="votesThisRound"/> is the full set of votes the
    /// repository returns for the round (must include the current voter's vote).
    /// </summary>
    private async Task<ActivityResult> ResumeOnce(
        string votingMode, int totalMembers, MajorityConfig majority, QuorumConfig quorum,
        (string voter, string vote)[] votesThisRound, string currentVoter, string currentVote,
        Guid appraisalId)
    {
        _clock.ApplicationNow.Returns(DateTime.UtcNow);

        var committee = Committee.Create("Committee", CommitteeCode, null,
            QuorumType.Fixed, quorum.Value, MajorityType.Simple);
        _committeeRepository.GetByCodeAsync(CommitteeCode, Arg.Any<CancellationToken>()).Returns(committee);

        var normalizedId = Normalize(ActivityId);
        var members = Enumerable.Range(1, totalMembers)
            .Select(i => new ApprovalMemberInfo($"voter{i}", "Member"))
            .ToList();

        var instance = WorkflowInstance.Create(Guid.NewGuid(), "Test Workflow", null, "system");
        instance.UpdateVariables(new Dictionary<string, object>
        {
            ["appraisalId"] = appraisalId,
            ["appraisalNumber"] = "2568-0001",
            [$"{normalizedId}_members"] = members,
            [$"{normalizedId}_quorum"] = quorum,
            [$"{normalizedId}_majority"] = majority,
            [$"{normalizedId}_votingMode"] = votingMode,
            [$"{normalizedId}_conditions"] = new List<ApprovalConditionInfo>(),
            [$"{normalizedId}_voteOptions"] = new List<string> { "approve", "reject", "route_back" },
            [$"{normalizedId}_committeeName"] = "Committee",
            [$"{normalizedId}_committeeCode"] = CommitteeCode,
            [$"{normalizedId}_totalMembers"] = totalMembers,
            ["activityName"] = ActivityId
        });

        var execution = WorkflowActivityExecution.Create(
            instance.Id, ActivityId, "Approval Activity", ActivityTypes.ApprovalActivity);
        execution.Start();
        instance.AddActivityExecution(execution);

        _voteRepository.HasMemberVotedAsync(execution.Id, currentVoter, Arg.Any<CancellationToken>())
            .Returns(false);

        var votes = votesThisRound
            .Select(v => ApprovalVote.Create(appraisalId, instance.Id, ActivityId, execution.Id,
                v.voter, "Member", v.vote, null))
            .ToList();
        _voteRepository.GetVotesForExecutionAsync(execution.Id, Arg.Any<CancellationToken>())
            .Returns(votes);

        var context = new ActivityContext
        {
            WorkflowInstanceId = instance.Id,
            ActivityId = ActivityId,
            ActivityName = "Approval Activity",
            WorkflowInstance = instance,
            Variables = new Dictionary<string, object>(instance.Variables),
            Properties = new Dictionary<string, object>()
        };

        return await BuildActivity().ResumeAsync(context,
            new Dictionary<string, object> { ["completedBy"] = currentVoter, ["decisionTaken"] = currentVote },
            CancellationToken.None);
    }

    // ── WaitForAll ───────────────────────────────────────────────────────────

    [Fact]
    public async Task WaitForAll_NotEveryoneVoted_StaysPending()
    {
        // 3 members, only 2 have voted (both approve) — must wait for the 3rd.
        var result = await ResumeOnce("WaitForAll", 3,
            new MajorityConfig("Unanimous", "approve"), new QuorumConfig("Fixed", 2),
            new[] { ("voter1", "approve"), ("voter2", "approve") }, "voter2", "approve",
            Guid.NewGuid());

        result.Status.Should().Be(ActivityResultStatus.Pending);
        _outbox.DidNotReceive().Publish(Arg.Any<AppraisalApprovedIntegrationEvent>(), Arg.Any<string>());
    }

    [Fact]
    public async Task WaitForAll_AllVotedAndUnanimous_Approves()
    {
        var appraisalId = Guid.NewGuid();
        var result = await ResumeOnce("WaitForAll", 3,
            new MajorityConfig("Unanimous", "approve"), new QuorumConfig("Fixed", 2),
            new[] { ("voter1", "approve"), ("voter2", "approve"), ("voter3", "approve") },
            "voter3", "approve", appraisalId);

        result.Status.Should().Be(ActivityResultStatus.Completed);
        _outbox.Received(1).Publish(
            Arg.Is<AppraisalApprovedIntegrationEvent>(e => e.AppraisalId == appraisalId),
            Arg.Any<string>());
        // Everyone voted, so no leftover-task cleanup.
        _publisher.DidNotReceive().Publish(Arg.Any<ApprovalRoundClosedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WaitForAll_AllVotedButNotApproved_DoesNothing()
    {
        // 3 members all voted but one rejected → unanimous not met → do nothing (stay pending).
        var result = await ResumeOnce("WaitForAll", 3,
            new MajorityConfig("Unanimous", "approve"), new QuorumConfig("Fixed", 2),
            new[] { ("voter1", "approve"), ("voter2", "approve"), ("voter3", "reject") },
            "voter3", "reject", Guid.NewGuid());

        result.Status.Should().Be(ActivityResultStatus.Pending);
        _outbox.DidNotReceive().Publish(Arg.Any<AppraisalApprovedIntegrationEvent>(), Arg.Any<string>());
    }

    // ── Quorum (with all-members denominator) ──────────────────────────────────

    [Fact]
    public async Task Quorum_SimpleMajorityOfAllMembers_DecidesEarlyAndClosesLeftovers()
    {
        // 5 members, Simple majority = needs 3 of 5. 3rd approve arrives (quorum 3) → decide now,
        // even though members 4 and 5 never voted; their tasks get closed.
        var appraisalId = Guid.NewGuid();
        var result = await ResumeOnce("Quorum", 5,
            new MajorityConfig("Simple", "approve"), new QuorumConfig("Fixed", 3),
            new[] { ("voter1", "approve"), ("voter2", "approve"), ("voter3", "approve") },
            "voter3", "approve", appraisalId);

        result.Status.Should().Be(ActivityResultStatus.Completed);
        _outbox.Received(1).Publish(
            Arg.Is<AppraisalApprovedIntegrationEvent>(e => e.AppraisalId == appraisalId),
            Arg.Any<string>());
        _publisher.Received(1).Publish(
            Arg.Is<ApprovalRoundClosedEvent>(e => e.ActivityId == ActivityId),
            Arg.Any<CancellationToken>());
    }

    // ── CheckMajority config-string parse path (L1) ────────────────────────────

    [Fact]
    public async Task Majority_UnknownType_DeniesAndNeverDecides()
    {
        // A garbage majority type must parse-fail → CheckMajority returns false → the round never
        // resolves even with full unanimous approval (matches the old `_ => false`).
        var result = await ResumeOnce("Quorum", 3,
            new MajorityConfig("garbage", "approve"), new QuorumConfig("Fixed", 1),
            new[] { ("voter1", "approve"), ("voter2", "approve"), ("voter3", "approve") },
            "voter3", "approve", Guid.NewGuid());

        result.Status.Should().Be(ActivityResultStatus.Pending);
        _outbox.DidNotReceive().Publish(Arg.Any<AppraisalApprovedIntegrationEvent>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Majority_TwoThirds_RoundTripsThroughConfigString()
    {
        // 5 members, "TwoThirds" needs 4. All vote, 4 approve + 1 reject → approves. Proves the
        // "TwoThirds" string parses to MajorityType and uses the all-members denominator.
        var appraisalId = Guid.NewGuid();
        var result = await ResumeOnce("WaitForAll", 5,
            new MajorityConfig("TwoThirds", "approve"), new QuorumConfig("Fixed", 3),
            new[]
            {
                ("voter1", "approve"), ("voter2", "approve"), ("voter3", "reject"),
                ("voter4", "approve"), ("voter5", "approve")
            },
            "voter5", "approve", appraisalId);

        result.Status.Should().Be(ActivityResultStatus.Completed);
        _outbox.Received(1).Publish(
            Arg.Is<AppraisalApprovedIntegrationEvent>(e => e.AppraisalId == appraisalId),
            Arg.Any<string>());
    }

    [Fact]
    public async Task Quorum_SimpleMajorityNotYetReached_StaysPending()
    {
        // 5 members, Simple needs 3; only 2 approve so far → not decided.
        var result = await ResumeOnce("Quorum", 5,
            new MajorityConfig("Simple", "approve"), new QuorumConfig("Fixed", 3),
            new[] { ("voter1", "approve"), ("voter2", "approve") }, "voter2", "approve",
            Guid.NewGuid());

        result.Status.Should().Be(ActivityResultStatus.Pending);
        _outbox.DidNotReceive().Publish(Arg.Any<AppraisalApprovedIntegrationEvent>(), Arg.Any<string>());
        _publisher.DidNotReceive().Publish(Arg.Any<ApprovalRoundClosedEvent>(), Arg.Any<CancellationToken>());
    }
}
