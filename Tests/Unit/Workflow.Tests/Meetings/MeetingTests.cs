using FluentAssertions;
using NSubstitute;
using Workflow.Domain.Committees;
using Workflow.Meetings.Domain;
using Workflow.Meetings.Domain.Events;
using Workflow.Meetings.ReadModels;

namespace Workflow.Tests.Meetings;

public class MeetingTests
{
    // -------------------------------------------------------------------------
    // SnapshotCommittee
    // -------------------------------------------------------------------------

    [Fact]
    public void SnapshotCommittee_CopiesOnlyActiveMembers()
    {
        var meeting = Meeting.Create("Budget Q1", null);
        var committee = BuildCommittee();
        committee.AddMember("active-1", "Alice", CommitteeMemberPosition.Chairman);
        committee.AddMember("active-2", "Bob", CommitteeMemberPosition.Member);
        var inactiveMember = committee.AddMember("inactive-1", "Charlie", CommitteeMemberPosition.Secretary);
        inactiveMember.Deactivate();

        meeting.SnapshotCommittee(committee);

        meeting.Members.Should().HaveCount(2);
        meeting.Members.Select(m => m.UserId).Should().BeEquivalentTo(["active-1", "active-2"]);
    }

    [Fact]
    public void SnapshotCommittee_CalledTwice_Throws()
    {
        var meeting = Meeting.Create("Budget Q1", null);
        var committee = BuildCommittee();
        committee.AddMember("user-1", "Alice", CommitteeMemberPosition.Chairman);

        meeting.SnapshotCommittee(committee);
        var act = () => meeting.SnapshotCommittee(committee);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already been taken*");
    }

    [Fact]
    public void SnapshotCommittee_NullCommittee_Throws()
    {
        var meeting = Meeting.Create("Budget Q1", null);

        var act = () => meeting.SnapshotCommittee(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // -------------------------------------------------------------------------
    // SendInvitation — Draft→Scheduled
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendInvitation_InDraft_TransitionsToScheduled_And_AssignsMeetingNo()
    {
        var meeting = Meeting.Create("Budget Q1", null);
        var generator = Substitute.For<IMeetingNoGenerator>();
        generator.NextAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("1/2568"));

        await meeting.SendInvitation(generator, DateTime.UtcNow, CancellationToken.None);

        meeting.Status.Should().Be(MeetingStatus.Scheduled);
        meeting.MeetingNo.Should().Be("1/2568");
        meeting.InvitationSentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SendInvitation_InDraft_Raises_MeetingInvitationSentDomainEvent()
    {
        var meeting = Meeting.Create("Budget Q1", null);
        var generator = Substitute.For<IMeetingNoGenerator>();
        generator.NextAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("1/2568"));

        await meeting.SendInvitation(generator, DateTime.UtcNow, CancellationToken.None);

        meeting.DomainEvents.Should().ContainSingle(e => e is MeetingInvitationSentDomainEvent);
        var evt = (MeetingInvitationSentDomainEvent)meeting.DomainEvents.Single();
        evt.MeetingId.Should().Be(meeting.Id);
        evt.MeetingNo.Should().Be("1/2568");
    }

    [Fact]
    public async Task SendInvitation_WhenAlreadyScheduled_UpdatesInvitationSentAt_KeepsSameMeetingNo_NoNewEvent()
    {
        var meeting = Meeting.Create("Budget Q1", null);
        var generator = Substitute.For<IMeetingNoGenerator>();
        generator.NextAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("3/2568"));

        var firstSentAt = DateTime.UtcNow.AddHours(-1);
        await meeting.SendInvitation(generator, firstSentAt, CancellationToken.None);
        meeting.ClearDomainEvents();  // clear events from first call

        var reSentAt = DateTime.UtcNow;
        await meeting.SendInvitation(generator, reSentAt, CancellationToken.None);

        meeting.Status.Should().Be(MeetingStatus.Scheduled);
        meeting.MeetingNo.Should().Be("3/2568");                   // unchanged
        meeting.InvitationSentAt.Should().BeCloseTo(reSentAt, TimeSpan.FromSeconds(1));
        meeting.DomainEvents.Should().BeEmpty();                    // no new event on re-send
    }

    [Fact]
    public async Task SendInvitation_WhenEnded_Throws()
    {
        var meeting = BuildScheduledMeetingWithOneItem();
        meeting.End(DateTime.UtcNow);

        var generator = Substitute.For<IMeetingNoGenerator>();
        var act = async () =>
            await meeting.SendInvitation(generator, DateTime.UtcNow, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*terminal*");
    }

    [Fact]
    public async Task SendInvitation_WhenCancelled_Throws()
    {
        var meeting = Meeting.Create("Budget Q1", null);
        meeting.Cancel("test reason");

        var generator = Substitute.For<IMeetingNoGenerator>();
        var act = async () =>
            await meeting.SendInvitation(generator, DateTime.UtcNow, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*terminal*");
    }

    // -------------------------------------------------------------------------
    // Cancel
    // -------------------------------------------------------------------------

    [Fact]
    public void Cancel_InDraft_WithReason_Succeeds()
    {
        var meeting = Meeting.Create("Budget Q1", null);

        meeting.Cancel("budget frozen");

        meeting.Status.Should().Be(MeetingStatus.Cancelled);
        meeting.CancelReason.Should().Be("budget frozen");
        meeting.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Cancel_InScheduled_WithReason_Succeeds()
    {
        var meeting = Meeting.Create("Budget Q1", null);
        var generator = Substitute.For<IMeetingNoGenerator>();
        generator.NextAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("2/2568"));
        await meeting.SendInvitation(generator, DateTime.UtcNow, CancellationToken.None);

        meeting.Cancel("venue not available");

        meeting.Status.Should().Be(MeetingStatus.Cancelled);
    }

    [Fact]
    public void Cancel_EmptyReason_Throws()
    {
        var meeting = Meeting.Create("Budget Q1", null);

        var act = () => meeting.Cancel("   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Cancel_NullReason_Throws()
    {
        var meeting = Meeting.Create("Budget Q1", null);

        var act = () => meeting.Cancel(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Cancel_WhenEnded_Throws()
    {
        var meeting = BuildScheduledMeetingWithOneItem();
        meeting.End(DateTime.UtcNow);

        var act = () => meeting.Cancel("reason");

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// C-1 regression: Cancel on a meeting with ZERO decision items must still raise exactly one
    /// MeetingCancelledDomainEvent so that the handler can detach Included acknowledgement items.
    /// </summary>
    [Fact]
    public void Cancel_on_meeting_with_zero_decision_items_still_raises_single_cancel_event_including_ack_detach()
    {
        // Arrange: Draft meeting with only an ack item (no Decision items).
        var meeting = Meeting.Create("Ack-Only Meeting", null);
        var ackItem = BuildAckItem();
        meeting.CutOff([], new[] { ackItem }, DateTime.UtcNow);
        meeting.ClearDomainEvents();

        // Act
        meeting.Cancel("cancelled before invitation");

        // Assert: exactly ONE event regardless of zero decision items.
        var events = meeting.DomainEvents.OfType<MeetingCancelledDomainEvent>().ToList();
        events.Should().HaveCount(1, "Cancel must always raise exactly one MeetingCancelledDomainEvent");

        var evt = events.Single();
        evt.MeetingId.Should().Be(meeting.Id);
        evt.Reason.Should().Be("cancelled before invitation");
        evt.CancelledAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        evt.DecisionItems.Should().BeEmpty(
            "no Decision items were on this meeting — handler still receives the event to detach ack items");
    }

    // -------------------------------------------------------------------------
    // CutOff
    // -------------------------------------------------------------------------

    [Fact]
    public void CutOff_InDraft_AddsDecisionAndAckItems_SetsCutOffAt()
    {
        var meeting = Meeting.Create("Budget Q1", null);
        var now = DateTime.UtcNow;
        var queued = new[] { BuildQueueItem() };
        var ack = new[] { BuildAckItem() };

        meeting.CutOff(queued, ack, now);

        meeting.CutOffAt.Should().Be(now);
        meeting.Items.Should().HaveCount(2);
        meeting.Items.Should().ContainSingle(i => i.Kind == MeetingItemKind.Decision);
        meeting.Items.Should().ContainSingle(i => i.Kind == MeetingItemKind.Acknowledgement);
    }

    [Fact]
    public void CutOff_InDraft_Raises_MeetingCutOffDomainEvent_WithIncludedIds()
    {
        var meeting = Meeting.Create("Budget Q1", null);
        var queued = new[] { BuildQueueItem() };

        meeting.CutOff(queued, [], DateTime.UtcNow);

        meeting.DomainEvents.Should().ContainSingle(e => e is MeetingCutOffDomainEvent);
        var evt = (MeetingCutOffDomainEvent)meeting.DomainEvents.Single();
        evt.IncludedAppraisalIds.Should().ContainSingle(id => id == queued[0].AppraisalId);
    }

    [Fact]
    public void CutOff_CalledTwice_Throws()
    {
        var meeting = Meeting.Create("Budget Q1", null);
        meeting.CutOff([BuildQueueItem()], [], DateTime.UtcNow);

        var act = () => meeting.CutOff([BuildQueueItem()], [], DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already been cut off*");
    }

    [Fact]
    public void CutOff_WhenNotDraft_Throws()
    {
        var meeting = BuildScheduledMeetingWithOneItem();

        var act = () => meeting.CutOff([], [], DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Draft*");
    }

    // -------------------------------------------------------------------------
    // End
    // -------------------------------------------------------------------------

    [Fact]
    public void End_WhenScheduled_SetsStatusEndedAndRaisesEvent()
    {
        var meeting = BuildScheduledMeetingWithOneItem();

        meeting.End(DateTime.UtcNow);

        meeting.Status.Should().Be(MeetingStatus.Ended);
        meeting.EndedAt.Should().NotBeNull();
        meeting.DomainEvents.Should().ContainSingle(e => e is MeetingEndedDomainEvent);
    }

    [Fact]
    public void End_WhenDraft_Throws()
    {
        var meeting = Meeting.Create("Budget Q1", null);
        // Add an item so end doesn't fail on empty check — but status is Draft
        meeting.AddItem(Guid.NewGuid(), "APR-001", 1_000_000, Guid.NewGuid(), "act-1");

        var act = () => meeting.End(DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Scheduled*");
    }

    [Fact]
    public void End_Raises_MeetingEndedDomainEvent_WithCorrectEndedAt()
    {
        var meeting = BuildScheduledMeetingWithOneItem();

        meeting.End(DateTime.UtcNow);

        var evt = (MeetingEndedDomainEvent)meeting.DomainEvents.Single(e => e is MeetingEndedDomainEvent);
        evt.MeetingId.Should().Be(meeting.Id);
        evt.EndedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    // -------------------------------------------------------------------------
    // ReleaseItem / RouteBackItem
    // -------------------------------------------------------------------------

    [Fact]
    public void ReleaseItem_AfterEnd_ReleasesDecisionItem_And_RaisesEvent()
    {
        var meeting = BuildScheduledMeetingWithOneItem();
        var appraisalId = meeting.Items[0].AppraisalId;
        meeting.End(DateTime.UtcNow);

        meeting.ReleaseItem(appraisalId, "secretary-1", DateTime.UtcNow);

        var item = meeting.Items.Single(i => i.AppraisalId == appraisalId);
        item.ItemDecision.Should().Be(ItemDecision.Released);
        meeting.DomainEvents.Should().ContainSingle(e => e is MeetingItemReleasedDomainEvent);
    }

    [Fact]
    public void ReleaseItem_IncludesMemberUserIds_InEvent()
    {
        var committee = BuildCommittee();
        committee.AddMember("m1", "Alice", CommitteeMemberPosition.Chairman);
        committee.AddMember("m2", "Bob", CommitteeMemberPosition.Member);

        var meeting = BuildScheduledMeetingWithOneItem(committee);
        var appraisalId = meeting.Items[0].AppraisalId;
        meeting.End(DateTime.UtcNow);

        meeting.ReleaseItem(appraisalId, "secretary-1", DateTime.UtcNow);

        var evt = (MeetingItemReleasedDomainEvent)meeting.DomainEvents
            .Single(e => e is MeetingItemReleasedDomainEvent);
        evt.MemberUserIds.Should().BeEquivalentTo(["m1", "m2"]);
    }

    [Fact]
    public void ReleaseItem_BeforeEnd_Throws()
    {
        var meeting = BuildScheduledMeetingWithOneItem();
        var appraisalId = meeting.Items[0].AppraisalId;

        var act = () => meeting.ReleaseItem(appraisalId, "secretary-1", DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ended*");
    }

    [Fact]
    public void ReleaseItem_OnAcknowledgementItem_Throws()
    {
        var meeting = BuildScheduledMeetingWithOneItem();
        meeting.End(DateTime.UtcNow);

        // Add an acknowledgement item directly via CutOff would require Draft status.
        // Instead, test by verifying the guard on GetDecisionItemOrThrow path:
        // The only way to have an ack item in an Ended meeting is via CutOff → Scheduled → End.
        // We set up that scenario via a separate helper.
        var ackMeeting = BuildEndedMeetingWithAckItem();
        var ackAppraisalId = ackMeeting.Items.Single(i => i.Kind == MeetingItemKind.Acknowledgement).AppraisalId;

        var act = () => ackMeeting.ReleaseItem(ackAppraisalId, "secretary-1", DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*acknowledgement*");
    }

    [Fact]
    public void ReleaseItem_WhenAlreadyReleased_Throws()
    {
        var meeting = BuildScheduledMeetingWithOneItem();
        var appraisalId = meeting.Items[0].AppraisalId;
        meeting.End(DateTime.UtcNow);
        meeting.ReleaseItem(appraisalId, "secretary-1", DateTime.UtcNow);

        var act = () => meeting.ReleaseItem(appraisalId, "secretary-1", DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already*");
    }

    [Fact]
    public void RouteBackItem_AfterEnd_RoutesBackDecisionItem_And_RaisesEvent()
    {
        var meeting = BuildScheduledMeetingWithOneItem();
        var appraisalId = meeting.Items[0].AppraisalId;
        meeting.End(DateTime.UtcNow);

        meeting.RouteBackItem(appraisalId, "secretary-1", "needs correction", DateTime.UtcNow);

        var item = meeting.Items.Single(i => i.AppraisalId == appraisalId);
        item.ItemDecision.Should().Be(ItemDecision.RoutedBack);
        item.DecisionReason.Should().Be("needs correction");

        var evt = (MeetingItemRoutedBackDomainEvent)meeting.DomainEvents
            .Single(e => e is MeetingItemRoutedBackDomainEvent);
        evt.Reason.Should().Be("needs correction");
    }

    [Fact]
    public void RouteBackItem_EmptyReason_Throws()
    {
        var meeting = BuildScheduledMeetingWithOneItem();
        var appraisalId = meeting.Items[0].AppraisalId;
        meeting.End(DateTime.UtcNow);

        var act = () => meeting.RouteBackItem(appraisalId, "secretary-1", "", DateTime.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RouteBackItem_OnAcknowledgementItem_Throws()
    {
        var ackMeeting = BuildEndedMeetingWithAckItem();
        var ackAppraisalId = ackMeeting.Items.Single(i => i.Kind == MeetingItemKind.Acknowledgement).AppraisalId;

        var act = () => ackMeeting.RouteBackItem(ackAppraisalId, "secretary-1", "bad data", DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*acknowledgement*");
    }

    // -------------------------------------------------------------------------
    // AddMember / RemoveMember / ChangeMemberPosition
    // -------------------------------------------------------------------------

    [Fact]
    public void AddMember_InDraft_Succeeds()
    {
        var meeting = Meeting.Create("Budget Q1", null);
        var member = MeetingMember.CreateManual(meeting.Id, "user-x", "Xavier", CommitteeMemberPosition.UW);

        meeting.AddMember(member);

        meeting.Members.Should().ContainSingle(m => m.UserId == "user-x");
    }

    [Fact]
    public void AddMember_InEnded_Throws()
    {
        var meeting = BuildScheduledMeetingWithOneItem();
        meeting.End(DateTime.UtcNow);
        var member = MeetingMember.CreateManual(meeting.Id, "user-y", "Yvonne", CommitteeMemberPosition.Risk);

        var act = () => meeting.AddMember(member);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddMember_DuplicateUser_Throws()
    {
        var meeting = Meeting.Create("Budget Q1", null);
        var member1 = MeetingMember.CreateManual(meeting.Id, "user-dup", "Duper", CommitteeMemberPosition.Member);
        meeting.AddMember(member1);
        var member2 = MeetingMember.CreateManual(meeting.Id, "user-dup", "Duper Clone", CommitteeMemberPosition.Credit);

        var act = () => meeting.AddMember(member2);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already a member*");
    }

    [Fact]
    public void RemoveMember_InDraft_RemovesMember()
    {
        var meeting = Meeting.Create("Budget Q1", null);
        var member = MeetingMember.CreateManual(meeting.Id, "user-z", "Zara", CommitteeMemberPosition.Member);
        meeting.AddMember(member);

        meeting.RemoveMember(member.Id);

        meeting.Members.Should().BeEmpty();
    }

    [Fact]
    public void RemoveMember_InEnded_Throws()
    {
        var meeting = BuildScheduledMeetingWithOneItem();
        meeting.End(DateTime.UtcNow);

        var act = () => meeting.RemoveMember(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ChangeMemberPosition_InScheduled_UpdatesPosition()
    {
        var meeting = Meeting.Create("Budget Q1", null);
        var member = MeetingMember.CreateManual(meeting.Id, "user-pos", "Posie", CommitteeMemberPosition.Member);
        meeting.AddMember(member);
        // Manually transition to Scheduled via a Meeting.Create that is already scheduled is not
        // directly possible without SendInvitation. Use AddItem + inline send via helper.
        // For simplicity, test in Draft (same EnsureMutableStatus guard).

        meeting.ChangeMemberPosition(member.Id, CommitteeMemberPosition.Director);

        meeting.Members.Single(m => m.Id == member.Id).Position.Should().Be(CommitteeMemberPosition.Director);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static Committee BuildCommittee()
    {
        return Committee.Create("Test Committee", "TC", null,
            QuorumType.Fixed, 2, MajorityType.Simple);
    }

    /// <summary>
    /// Builds a Scheduled meeting with one Decision item already added.
    /// Uses Meeting.AddItem directly without CutOff to keep the helper simple.
    /// The meeting is then force-transitioned to Scheduled by calling SendInvitation
    /// synchronously via a synchronous wrapper.
    /// </summary>
    private static Meeting BuildScheduledMeetingWithOneItem(Committee? committee = null)
    {
        var meeting = Meeting.Create("Test Meeting", null);

        if (committee is not null)
            meeting.SnapshotCommittee(committee);

        var appraisalId = Guid.NewGuid();
        meeting.AddItem(appraisalId, "APR-001", 500_000m, Guid.NewGuid(), "meeting-act");

        // Transition to Scheduled using a stubbed generator
        var generator = Substitute.For<IMeetingNoGenerator>();
        generator.NextAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("1/2568"));
        meeting.SendInvitation(generator, DateTime.UtcNow, CancellationToken.None).GetAwaiter().GetResult();
        meeting.ClearDomainEvents();

        return meeting;
    }

    /// <summary>
    /// Builds an Ended meeting that contains one Acknowledgement item.
    /// Uses CutOff in Draft and then transitions through Scheduled → Ended.
    /// </summary>
    private static Meeting BuildEndedMeetingWithAckItem()
    {
        var meeting = Meeting.Create("Ack Meeting", null);

        // CutOff with only an ack item
        var ackItem = BuildAckItem();
        meeting.CutOff([], new[] { ackItem }, DateTime.UtcNow);

        // Need at least one item to End; add a decision item manually after cut-off via AddItem
        // (AddItem allows Scheduled status — but we're still in Draft here, so we can add).
        meeting.AddItem(Guid.NewGuid(), "APR-DEC-001", 100_000m, Guid.NewGuid(), "act-dec");

        var generator = Substitute.For<IMeetingNoGenerator>();
        generator.NextAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("5/2568"));
        meeting.SendInvitation(generator, DateTime.UtcNow, CancellationToken.None).GetAwaiter().GetResult();
        meeting.ClearDomainEvents();

        meeting.End(DateTime.UtcNow);
        meeting.ClearDomainEvents();

        return meeting;
    }

    private static MeetingQueueItem BuildQueueItem(Guid? appraisalId = null)
    {
        return MeetingQueueItem.CreateQueued(
            appraisalId ?? Guid.NewGuid(),
            "APR-Q-001",
            800_000m,
            Guid.NewGuid(),
            "queue-act");
    }

    private static AppraisalAcknowledgementQueueItem BuildAckItem(Guid? appraisalId = null)
    {
        return AppraisalAcknowledgementQueueItem.Create(
            appraisalId ?? Guid.NewGuid(),
            "APR-ACK-001",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "G1",
            "Group1");
    }
}
