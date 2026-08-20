using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared.Data.Outbox;
using Shared.Time;
using Workflow.Data;
using Workflow.Data.Repository;
using Workflow.Domain;
using Workflow.Domain.Committees;
using Workflow.Sla.Services;
using Workflow.Workflow.Activities;
using Workflow.Workflow.Activities.Approval;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;
using Xunit;

namespace Workflow.Tests.Workflow.Activities;

/// <summary>
/// Pins WHERE the role behind a committee RoleRequired condition comes from.
///
/// It is NOT re-read from the committee configuration table when a vote is cast. The role is
/// taken from the members snapshot the round started with — the meeting roster, for an appraisal
/// that came through a meeting — written onto <see cref="ApprovalVote.MemberRole"/>, and the
/// condition is then evaluated against those vote rows. So the UW who satisfies
/// "a UW must approve" is the UW on that meeting, not the UW configured on the committee.
/// </summary>
public class ApprovalActivityMeetingRoleConditionTests
{
    private readonly IApprovalMemberResolver _memberResolver = Substitute.For<IApprovalMemberResolver>();
    private readonly IApprovalVoteRepository _voteRepository = Substitute.For<IApprovalVoteRepository>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly IIntegrationEventOutbox _outbox = Substitute.For<IIntegrationEventOutbox>();
    private readonly ICommitteeRepository _committeeRepository = Substitute.For<ICommitteeRepository>();
    private readonly WorkflowDbContext _dbContext = new(
        new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase($"approval-role-condition-{Guid.NewGuid()}")
            .Options);
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();

    private const string ActivityId = "pending-approval";

    /// <summary>
    /// The roster of the meeting that released the appraisal. Deliberately NOT the committee's
    /// configured membership — "meeting-uw" is the only member holding UW.
    /// </summary>
    private static readonly (string Voter, string Role)[] MeetingRoster =
    [
        ("meeting-chair", nameof(CommitteeMemberPosition.Chairman)),
        ("meeting-member", nameof(CommitteeMemberPosition.Member)),
        ("meeting-uw", nameof(CommitteeMemberPosition.UW))
    ];

    public ApprovalActivityMeetingRoleConditionTests()
        => _clock.ApplicationNow.Returns(DateTime.UtcNow);

    private ApprovalActivity BuildActivity() =>
        new(_memberResolver, _voteRepository, _publisher, _outbox, _committeeRepository,
            _dbContext, _clock, Substitute.For<ISlaCalculator>(),
            Substitute.For<ILogger<ApprovalActivity>>());

    [Fact]
    public async Task Resume_QuorumAndMajorityMetButNoUwVote_DoesNotResolve()
    {
        // Two of three approve: quorum (2) and Simple majority (2 > 3/2) are both satisfied.
        // Only the committee's RoleRequired UW condition is outstanding.
        var result = await Resume(
            currentVoter: "meeting-member",
            votesThisRound: [("meeting-chair", "approve"), ("meeting-member", "approve")]);

        result.Status.Should().Be(ActivityResultStatus.Pending);
        result.OutputData.Should().NotContainKey("decision");
    }

    [Fact]
    public async Task Resume_OnceTheMeetingsUwApproves_Resolves()
    {
        var result = await Resume(
            currentVoter: "meeting-uw",
            votesThisRound:
            [
                ("meeting-chair", "approve"),
                ("meeting-member", "approve"),
                ("meeting-uw", "approve")
            ]);

        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData["decision"].Should().Be("approve");
    }

    [Fact]
    public async Task Resume_WritesTheMeetingPositionOntoTheVote()
    {
        // This is the value the condition is later matched on, and the value the approval-history
        // and approval-status views display as the approver's role.
        await Resume(
            currentVoter: "meeting-uw",
            votesThisRound: [("meeting-uw", "approve")]);

        var written = _voteRepository.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IApprovalVoteRepository.AddVoteAsync))
            .Select(c => (ApprovalVote)c.GetArguments()[0]!)
            .Single();

        written.Member.Should().Be("meeting-uw");
        written.MemberRole.Should().Be(nameof(CommitteeMemberPosition.UW));
    }

    [Fact]
    public async Task Resume_VoterOutsideTheMeetingRoster_IsRejected()
    {
        // A committee member who was removed from this meeting can no longer vote on it.
        var result = await Resume(
            currentVoter: "committee-only-member",
            votesThisRound: []);

        result.Status.Should().Be(ActivityResultStatus.Failed);
        result.ErrorMessage.Should().Contain("not a member of this approval group");
    }

    // -- Harness --

    private async Task<ActivityResult> Resume(
        string currentVoter,
        (string Voter, string Vote)[] votesThisRound)
    {
        var normalizedId = ActivityId.Replace("-", "_").Replace(" ", "_").Replace(".", "_").ToLowerInvariant();
        var appraisalId = Guid.NewGuid();

        var instance = WorkflowInstance.Create(Guid.NewGuid(), "Test Workflow", null, "system");
        instance.UpdateVariables(new Dictionary<string, object>
        {
            ["appraisalId"] = appraisalId,
            // The snapshot ApprovalActivity took at Execute — the meeting roster, with each
            // member's meeting position as their approval role.
            [$"{normalizedId}_members"] = MeetingRoster
                .Select(m => new ApprovalMemberInfo(m.Voter, m.Role))
                .ToList(),
            [$"{normalizedId}_quorum"] = new QuorumConfig("Fixed", 2),
            [$"{normalizedId}_majority"] = new MajorityConfig("Simple", "approve"),
            [$"{normalizedId}_votingMode"] = "Quorum",
            // Straight from the committee configuration — the seeded COMMITTEE_WITH_MEETING rule.
            [$"{normalizedId}_conditions"] = new List<ApprovalConditionInfo>
            {
                new(nameof(ConditionType.RoleRequired), nameof(CommitteeMemberPosition.UW), null)
            },
            [$"{normalizedId}_voteOptions"] = new List<string> { "approve", "reject", "route_back" },
            [$"{normalizedId}_committeeCode"] = "COMMITTEE_WITH_MEETING",
            [$"{normalizedId}_totalMembers"] = MeetingRoster.Length,
            ["activityName"] = "PendingApproval"
        });

        var execution = WorkflowActivityExecution.Create(
            instance.Id, ActivityId, "Approval Activity", ActivityTypes.ApprovalActivity);
        execution.Start();
        instance.AddActivityExecution(execution);

        _voteRepository.HasMemberVotedAsync(execution.Id, currentVoter, Arg.Any<CancellationToken>())
            .Returns(false);

        // Vote rows carry the role the activity itself would have stamped — the roster position.
        _voteRepository.GetVotesForExecutionAsync(execution.Id, Arg.Any<CancellationToken>())
            .Returns(votesThisRound
                .Select(v => ApprovalVote.Create(
                    appraisalId, instance.Id, ActivityId, execution.Id,
                    v.Voter, RoleOf(v.Voter), v.Vote, null))
                .ToList());

        var context = new ActivityContext
        {
            WorkflowInstanceId = instance.Id,
            ActivityId = ActivityId,
            ActivityName = "Committee Approval",
            WorkflowInstance = instance,
            Variables = new Dictionary<string, object>(instance.Variables),
            Properties = new Dictionary<string, object>()
        };

        return await BuildActivity().ResumeAsync(context,
            new Dictionary<string, object>
            {
                ["completedBy"] = currentVoter,
                ["decisionTaken"] = "approve"
            },
            CancellationToken.None);
    }

    private static string? RoleOf(string voter) =>
        MeetingRoster.FirstOrDefault(m => m.Voter == voter).Role;
}
