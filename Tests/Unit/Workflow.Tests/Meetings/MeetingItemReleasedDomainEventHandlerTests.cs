using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Workflow.Domain.Committees;
using Workflow.Meetings.Domain;
using Workflow.Meetings.Domain.Events;
using Workflow.Meetings.EventHandlers;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Services;
using Xunit;

namespace Workflow.Tests.Meetings;

/// <summary>
/// The handler is the hand-off point between the meeting roster and the approval round:
/// whatever it puts in the resume input is what MeetingActivity propagates and
/// ApprovalActivity consumes as its member list.
/// </summary>
public class MeetingItemReleasedDomainEventHandlerTests
{
    private readonly IWorkflowService _workflowService = Substitute.For<IWorkflowService>();

    private MeetingItemReleasedDomainEventHandler BuildHandler() =>
        new(_workflowService, Substitute.For<ILogger<MeetingItemReleasedDomainEventHandler>>());

    [Fact]
    public async Task Handle_ResumesWithRosterAsMeetingMemberOverrides()
    {
        var handler = BuildHandler();
        var workflowInstanceId = Guid.NewGuid();
        var notification = new MeetingItemReleasedDomainEvent(
            MeetingId: Guid.NewGuid(),
            AppraisalId: Guid.NewGuid(),
            WorkflowInstanceId: workflowInstanceId,
            ActivityId: "pending-meeting",
            ReleasedBy: "secretary",
            Members:
            [
                new MeetingApprover("alice", nameof(CommitteeMemberPosition.Chairman)),
                new MeetingApprover("bob", nameof(CommitteeMemberPosition.UW))
            ]);

        await handler.Handle(notification, CancellationToken.None);

        var input = CapturedInput();

        input.Should().ContainKey("meetingMemberOverrides");
        input.Should().NotContainKey("meetingMemberUserIds",
            "the approval activity reads meetingMemberOverrides; a user-id-only payload has no consumer");
        input["meetingOutcome"].Should().Be(MeetingOutcomes.Released);
        input["completedBy"].Should().Be("secretary");
    }

    [Fact]
    public async Task Handle_OverridesCarryUserIdAndRole_InTheShapeApprovalActivityDeserializes()
    {
        var handler = BuildHandler();
        var notification = new MeetingItemReleasedDomainEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "pending-meeting", "secretary",
            [
                new MeetingApprover("alice", nameof(CommitteeMemberPosition.Chairman)),
                new MeetingApprover("bob", nameof(CommitteeMemberPosition.UW))
            ]);

        await handler.Handle(notification, CancellationToken.None);

        var input = CapturedInput();

        // Variables round-trip through JSON before ApprovalActivity reads them, so assert on the
        // serialized shape rather than the CLR type: camelCase userId/role is what the activity's
        // case-insensitive deserializer binds to MeetingMemberOverride(UserId, Role).
        var roundTripped = JsonSerializer.Deserialize<List<RoundTrippedOverride>>(
            JsonSerializer.Serialize(input["meetingMemberOverrides"]),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        roundTripped.Should().BeEquivalentTo(
        [
            new RoundTrippedOverride("alice", nameof(CommitteeMemberPosition.Chairman)),
            new RoundTrippedOverride("bob", nameof(CommitteeMemberPosition.UW))
        ]);
    }

    [Fact]
    public async Task Handle_ResumesTheReleasedItemsInstanceAndActivity()
    {
        var handler = BuildHandler();
        var workflowInstanceId = Guid.NewGuid();
        var notification = new MeetingItemReleasedDomainEvent(
            Guid.NewGuid(), Guid.NewGuid(), workflowInstanceId, "pending-meeting", "secretary",
            [new MeetingApprover("alice", nameof(CommitteeMemberPosition.Chairman))]);

        await handler.Handle(notification, CancellationToken.None);

        await _workflowService.Received(1).ResumeWorkflowAsync(
            workflowInstanceId,
            "pending-meeting",
            "secretary",
            Arg.Any<Dictionary<string, object>>(),
            Arg.Any<Dictionary<string, RuntimeOverride>>(),
            Arg.Any<CancellationToken>());
    }

    private Dictionary<string, object> CapturedInput()
    {
        var resume = _workflowService.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(IWorkflowService.ResumeWorkflowAsync));
        return (Dictionary<string, object>)resume.GetArguments()[3]!;
    }

    private record RoundTrippedOverride(string UserId, string Role);
}
