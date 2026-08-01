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
using Workflow.Sla.Services;
using Workflow.Workflow.Activities;
using Workflow.Workflow.Activities.Approval;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Events;
using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;
using Xunit;

namespace Workflow.Tests.Workflow.Activities;

/// <summary>
/// When an appraisal reaches approval through a meeting, the members are the meeting's roster —
/// not the committee's configured members — so per-meeting add/remove/position edits decide who
/// votes. Quorum, majority and conditions still come from the committee.
/// </summary>
public class ApprovalActivityMemberOverrideTests
{
    private readonly IApprovalMemberResolver _memberResolver = Substitute.For<IApprovalMemberResolver>();
    private readonly IApprovalVoteRepository _voteRepository = Substitute.For<IApprovalVoteRepository>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly IIntegrationEventOutbox _outbox = Substitute.For<IIntegrationEventOutbox>();
    private readonly ICommitteeRepository _committeeRepository = Substitute.For<ICommitteeRepository>();
    private readonly WorkflowDbContext _dbContext = new(
        new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase($"approval-override-{Guid.NewGuid()}")
            .Options);
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();

    private const string ActivityId = "pending-approval";

    public ApprovalActivityMemberOverrideTests()
    {
        _clock.ApplicationNow.Returns(DateTime.UtcNow);

        // The committee the workflow would otherwise approve with.
        _memberResolver.ResolveMembersAsync(
                Arg.Any<MemberSourceConfig>(), Arg.Any<Dictionary<string, object>>(),
                Arg.Any<QuorumConfig?>(), Arg.Any<MajorityConfig?>(), Arg.Any<CancellationToken>())
            .Returns(new ApprovalGroupInfo(
                Members:
                [
                    new ApprovalMemberInfo("committee-1", nameof(CommitteeMemberPosition.Chairman)),
                    new ApprovalMemberInfo("committee-2", nameof(CommitteeMemberPosition.Member))
                ],
                Quorum: new QuorumConfig("Fixed", 2),
                Majority: new MajorityConfig("Unanimous", "approve"),
                Conditions: [],
                CommitteeName: "Committee With Meeting",
                CommitteeCode: "COMMITTEE_WITH_MEETING"));
    }

    private ApprovalActivity BuildActivity() =>
        new(_memberResolver, _voteRepository, _publisher, _outbox, _committeeRepository,
            _dbContext, _clock, Substitute.For<ISlaCalculator>(),
            Substitute.For<ILogger<ApprovalActivity>>());

    [Fact]
    public async Task Execute_WithMeetingRoster_AssignsTasksToTheRosterInsteadOfTheCommittee()
    {
        var context = BuildContext(WithOverride(
            ("meeting-1", nameof(CommitteeMemberPosition.Chairman)),
            ("meeting-2", nameof(CommitteeMemberPosition.UW)),
            ("meeting-3", nameof(CommitteeMemberPosition.Member))));

        await BuildActivity().ExecuteAsync(context, CancellationToken.None);

        var assigned = CapturedAssignment();
        assigned.Members.Select(m => m.Username).Should()
            .BeEquivalentTo(["meeting-1", "meeting-2", "meeting-3"]);
    }

    [Fact]
    public async Task Execute_WithMeetingRoster_SnapshotsMeetingPositionsAsApprovalRoles()
    {
        var context = BuildContext(WithOverride(
            ("meeting-1", nameof(CommitteeMemberPosition.Chairman)),
            ("meeting-2", nameof(CommitteeMemberPosition.UW))));

        var result = await BuildActivity().ExecuteAsync(context, CancellationToken.None);

        // The snapshot is what each vote's MemberRole is taken from, and what committee
        // RoleRequired conditions are matched against.
        var members = (List<ApprovalMemberInfo>)result.OutputData[$"{Normalized}_members"];
        members.Should().BeEquivalentTo(
        [
            new ApprovalMemberInfo("meeting-1", nameof(CommitteeMemberPosition.Chairman)),
            new ApprovalMemberInfo("meeting-2", nameof(CommitteeMemberPosition.UW))
        ]);
        result.OutputData[$"{Normalized}_totalMembers"].Should().Be(2);
    }

    [Fact]
    public async Task Execute_WithMeetingRoster_KeepsQuorumAndMajorityFromTheCommittee()
    {
        var context = BuildContext(WithOverride(
            ("meeting-1", nameof(CommitteeMemberPosition.Chairman)),
            ("meeting-2", nameof(CommitteeMemberPosition.UW))));

        var result = await BuildActivity().ExecuteAsync(context, CancellationToken.None);

        result.OutputData[$"{Normalized}_quorum"].Should().BeEquivalentTo(new QuorumConfig("Fixed", 2));
        result.OutputData[$"{Normalized}_majority"].Should()
            .BeEquivalentTo(new MajorityConfig("Unanimous", "approve"));
    }

    [Fact]
    public async Task Execute_WithMeetingRoster_ClearsTheOverrideSoALaterNonMeetingRoundCannotInheritIt()
    {
        var context = BuildContext(WithOverride(("meeting-1", nameof(CommitteeMemberPosition.Chairman))));

        var result = await BuildActivity().ExecuteAsync(context, CancellationToken.None);

        // meetingMemberOverrides is a global variable: a route_back here can send the appraisal
        // back for rework and return it to approval through a tier that has no meeting.
        result.OutputData.Should().ContainKey("meetingMemberOverrides");
        ((List<ApprovalActivity.MeetingMemberOverride>)result.OutputData["meetingMemberOverrides"])
            .Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_WithoutMeetingRoster_UsesTheCommitteeAndWritesNoOverride()
    {
        var context = BuildContext(new Dictionary<string, object>());

        var result = await BuildActivity().ExecuteAsync(context, CancellationToken.None);

        CapturedAssignment().Members.Select(m => m.Username).Should()
            .BeEquivalentTo(["committee-1", "committee-2"]);
        result.OutputData.Should().NotContainKey("meetingMemberOverrides");
    }

    // -- Helpers --

    private static string Normalized =>
        ActivityId.Replace("-", "_").Replace(" ", "_").Replace(".", "_").ToLowerInvariant();

    private static Dictionary<string, object> WithOverride(
        params (string UserId, string Role)[] roster) =>
        new()
        {
            ["meetingMemberOverrides"] = roster
                .Select(m => new ApprovalActivity.MeetingMemberOverride(m.UserId, m.Role))
                .ToList()
        };

    private static ActivityContext BuildContext(Dictionary<string, object> variables)
    {
        var instance = WorkflowInstance.Create(Guid.NewGuid(), "Test Workflow", null, "system");
        instance.UpdateVariables(new Dictionary<string, object>(variables)
        {
            ["appraisalId"] = Guid.NewGuid(),
            ["appraisalValue"] = 50_000_000m
        });

        return new ActivityContext
        {
            WorkflowInstanceId = instance.Id,
            ActivityId = ActivityId,
            ActivityName = "Committee Approval",
            WorkflowInstance = instance,
            Variables = new Dictionary<string, object>(instance.Variables),
            Properties = new Dictionary<string, object>
            {
                ["memberSource"] = new MemberSourceConfig("committee", null, "COMMITTEE_WITH_MEETING", null, null, null),
                ["activityName"] = "PendingApproval"
            }
        };
    }

    private ApprovalTasksAssignedEvent CapturedAssignment() =>
        _publisher.ReceivedCalls()
            .Select(c => c.GetArguments()[0])
            .OfType<ApprovalTasksAssignedEvent>()
            .Single();
}
