using FluentAssertions;
using NSubstitute;
using Workflow.Domain.Committees;
using Workflow.Meetings.Domain;
using Workflow.Meetings.Domain.Events;
using Workflow.Meetings.ReadModels;

namespace Workflow.Tests.Meetings;

/// <summary>
/// Unit tests for the Meeting aggregate covering the new lifecycle:
/// New → InvitationSent → [InProgress time-derived] → Ended (auto) / RoutedBack → Ended
/// plus cancellation from New and InvitationSent.
/// </summary>
public class MeetingTests
{
    // =========================================================================
    // Meeting.Create
    // =========================================================================

    [Fact]
    public void Create_PopulatesMeetingNo_Seq_Year_And_StartsInNew()
    {
        var meeting = Meeting.Create("Budget Q1", notes: null, "1/2568", meetingNoSeq: 1, meetingNoYear: 2568);

        meeting.MeetingNo.Should().Be("1/2568");
        meeting.MeetingNoSeq.Should().Be(1);
        meeting.MeetingNoYear.Should().Be(2568);
        meeting.Status.Should().Be(MeetingStatus.New);
        meeting.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_TitleIsNullOrWhiteSpace_Throws()
    {
        var act = () => Meeting.Create("  ", null, "1/2568", 1, 2568);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_MeetingNoIsNullOrWhiteSpace_Throws()
    {
        var act = () => Meeting.Create("Title", null, "", 1, 2568);
        act.Should().Throw<ArgumentException>();
    }

    // =========================================================================
    // SnapshotCommittee
    // =========================================================================

    [Fact]
    public void SnapshotCommittee_CopiesOnlyActiveMembers_MatchingSeqParity()
    {
        var meeting = BuildNewMeeting();
        var committee = BuildCommittee();
        committee.AddMember("active-1", "Alice", CommitteeMemberPosition.Chairman);
        committee.AddMember("active-2", "Bob", CommitteeMemberPosition.Member);
        var inactiveMember = committee.AddMember("inactive-1", "Charlie", CommitteeMemberPosition.Secretary);
        inactiveMember.Deactivate();

        meeting.SnapshotCommittee(committee, meetingSeq: 1);

        meeting.Members.Should().HaveCount(2);
        meeting.Members.Select(m => m.UserId).Should().BeEquivalentTo(["active-1", "active-2"]);
    }

    [Fact]
    public void SnapshotCommittee_CalledTwice_Throws()
    {
        var meeting = BuildNewMeeting();
        var committee = BuildCommittee();
        committee.AddMember("user-1", "Alice", CommitteeMemberPosition.Chairman);

        meeting.SnapshotCommittee(committee, meetingSeq: 1);
        var act = () => meeting.SnapshotCommittee(committee, meetingSeq: 1);

        act.Should().Throw<InvalidOperationException>().WithMessage("*already been taken*");
    }

    [Fact]
    public void SnapshotCommittee_NullCommittee_Throws()
    {
        var meeting = BuildNewMeeting();
        var act = () => meeting.SnapshotCommittee(null!, meetingSeq: 1);
        act.Should().Throw<ArgumentNullException>();
    }

    // =========================================================================
    // SendInvitation
    // =========================================================================

    [Fact]
    public void SendInvitation_InNew_TransitionsToInvitationSent()
    {
        var meeting = BuildNewMeeting();
        var now = DateTime.UtcNow;

        meeting.SendInvitation(now);

        meeting.Status.Should().Be(MeetingStatus.InvitationSent);
        meeting.InvitationSentAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void SendInvitation_InNew_MeetingNoIsUnchanged()
    {
        var meeting = BuildNewMeeting(); // meetingNo = "1/2568"

        meeting.SendInvitation(DateTime.UtcNow);

        meeting.MeetingNo.Should().Be("1/2568");
    }

    [Fact]
    public void SendInvitation_InNew_Raises_MeetingInvitationSentDomainEvent()
    {
        var meeting = BuildNewMeeting();

        meeting.SendInvitation(DateTime.UtcNow);

        meeting.DomainEvents.Should().ContainSingle(e => e is MeetingInvitationSentDomainEvent);
        var evt = (MeetingInvitationSentDomainEvent)meeting.DomainEvents.Single();
        evt.MeetingId.Should().Be(meeting.Id);
        evt.MeetingNo.Should().Be("1/2568");
    }

    [Fact]
    public void SendInvitation_WhenAlreadyInvitationSent_UpdatesTimestamp_NoNewEvent()
    {
        var meeting = BuildNewMeeting();
        var firstAt = DateTime.UtcNow.AddHours(-1);
        meeting.SendInvitation(firstAt);
        meeting.ClearDomainEvents();

        var reSentAt = DateTime.UtcNow;
        meeting.SendInvitation(reSentAt);

        meeting.Status.Should().Be(MeetingStatus.InvitationSent);
        meeting.MeetingNo.Should().Be("1/2568");   // unchanged
        meeting.InvitationSentAt.Should().BeCloseTo(reSentAt, TimeSpan.FromSeconds(1));
        meeting.DomainEvents.Should().BeEmpty();    // no new event on re-send
    }

    [Fact]
    public void SendInvitation_WhenEnded_Throws()
    {
        var meeting = BuildInvitationSentMeetingWithOneItem();
        // Release all decision items to auto-end
        var appraisalId = meeting.Items.Single(i => i.Kind == MeetingItemKind.Decision).AppraisalId;
        meeting.ReleaseItem(appraisalId, "secretary", DateTime.UtcNow);

        var act = () => meeting.SendInvitation(DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>().WithMessage("*terminal*");
    }

    [Fact]
    public void SendInvitation_WhenCancelled_Throws()
    {
        var meeting = BuildNewMeeting();
        meeting.Cancel("test reason", DateTime.UtcNow);

        var act = () => meeting.SendInvitation(DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>().WithMessage("*terminal*");
    }

    // =========================================================================
    // CutOff
    // =========================================================================

    [Fact]
    public void CutOff_InNew_AddsItems_SetsCutOffAt()
    {
        var meeting = BuildNewMeeting();
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
    public void CutOff_InInvitationSent_AddsItems_SetsCutOffAt()
    {
        var meeting = BuildNewMeeting();
        meeting.SendInvitation(DateTime.UtcNow.AddDays(-1));
        meeting.ClearDomainEvents();
        var now = DateTime.UtcNow;

        meeting.CutOff([BuildQueueItem()], [], now);

        meeting.CutOffAt.Should().Be(now);
        meeting.Items.Should().HaveCount(1);
    }

    [Fact]
    public void CutOff_CalledTwice_IsAdditive_PicksUpNewItems()
    {
        var meeting = BuildNewMeeting();
        var item1 = BuildQueueItem();
        meeting.CutOff([item1], [], DateTime.UtcNow);
        meeting.ClearDomainEvents();

        // Second call with a new item — item1 is already on the meeting and is skipped.
        var item2 = BuildQueueItem();
        meeting.CutOff([item1, item2], [], DateTime.UtcNow);

        // Both items present; no duplicate of item1.
        meeting.Items.Should().HaveCount(2);
        meeting.Items.Select(i => i.AppraisalId)
            .Should().BeEquivalentTo([item1.AppraisalId, item2.AppraisalId]);
    }

    [Fact]
    public void CutOff_Raises_MeetingCutOffDomainEvent_EachCall()
    {
        var meeting = BuildNewMeeting();
        meeting.CutOff([BuildQueueItem()], [], DateTime.UtcNow);
        meeting.ClearDomainEvents();

        meeting.CutOff([BuildQueueItem()], [], DateTime.UtcNow);

        meeting.DomainEvents.Should().ContainSingle(e => e is MeetingCutOffDomainEvent);
    }

    [Fact]
    public void CutOff_WhenEnded_Throws()
    {
        var meeting = BuildInvitationSentMeetingWithOneItem();
        var appraisalId = meeting.Items.Single(i => i.Kind == MeetingItemKind.Decision).AppraisalId;
        meeting.ReleaseItem(appraisalId, "secretary", DateTime.UtcNow);

        var act = () => meeting.CutOff([], [], DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>().WithMessage("*New or InvitationSent*");
    }

    // =========================================================================
    // ReleaseItem — auto-transition to Ended
    // =========================================================================

    [Fact]
    public void ReleaseItem_LastPendingDecisionItem_AutoTransitionsToEnded()
    {
        var meeting = BuildInvitationSentMeetingWithOneItem();
        var appraisalId = meeting.Items.Single(i => i.Kind == MeetingItemKind.Decision).AppraisalId;

        meeting.ReleaseItem(appraisalId, "secretary", DateTime.UtcNow);

        meeting.Status.Should().Be(MeetingStatus.Ended);
        meeting.EndedAt.Should().NotBeNull();
        meeting.DomainEvents.Should().Contain(e => e is MeetingEndedDomainEvent);
    }

    [Fact]
    public void ReleaseItem_NotLastItem_DoesNotEndMeeting()
    {
        var meeting = BuildNewMeeting();
        var appraisalId1 = Guid.NewGuid();
        var appraisalId2 = Guid.NewGuid();
        meeting.AddItem(appraisalId1, "APR-001", 500_000m, Guid.NewGuid(), "act-1", DateTime.UtcNow);
        meeting.AddItem(appraisalId2, "APR-002", 600_000m, Guid.NewGuid(), "act-2", DateTime.UtcNow);
        meeting.SendInvitation(DateTime.UtcNow.AddDays(-1));
        meeting.ClearDomainEvents();

        meeting.ReleaseItem(appraisalId1, "secretary", DateTime.UtcNow);

        meeting.Status.Should().Be(MeetingStatus.InvitationSent);
        meeting.DomainEvents.Should().NotContain(e => e is MeetingEndedDomainEvent);
    }

    [Fact]
    public void ReleaseItem_AfterRouteBack_WhenAllReleased_AutoTransitionsToEnded()
    {
        var meeting = BuildNewMeeting();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        meeting.AddItem(id1, "APR-001", 500_000m, Guid.NewGuid(), "act-1", DateTime.UtcNow);
        meeting.AddItem(id2, "APR-002", 600_000m, Guid.NewGuid(), "act-2", DateTime.UtcNow);
        meeting.SendInvitation(DateTime.UtcNow.AddDays(-1));
        meeting.ClearDomainEvents();

        // Route back one item → status becomes RoutedBack
        meeting.RouteBackItem(id1, "secretary", "needs rework", DateTime.UtcNow);
        meeting.Status.Should().Be(MeetingStatus.RoutedBack);
        meeting.ClearDomainEvents();

        // Releasing second item — but id1 is RoutedBack, so NOT all are Released; meeting stays RoutedBack.
        meeting.ReleaseItem(id2, "secretary", DateTime.UtcNow);
        meeting.Status.Should().Be(MeetingStatus.RoutedBack,
            "a RoutedBack item still blocks Ended");
        meeting.DomainEvents.Should().NotContain(e => e is MeetingEndedDomainEvent);
    }

    [Fact]
    public void ReleaseItem_InNew_Throws()
    {
        var meeting = BuildNewMeeting();
        var appraisalId = Guid.NewGuid();
        meeting.AddItem(appraisalId, "APR-001", 500_000m, Guid.NewGuid(), "act-1", DateTime.UtcNow);

        var act = () => meeting.ReleaseItem(appraisalId, "secretary", DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>().WithMessage("*InvitationSent or RoutedBack*");
    }

    [Fact]
    public void ReleaseItem_IncludesMemberUserIds_InEvent()
    {
        var committee = BuildCommittee();
        committee.AddMember("m1", "Alice", CommitteeMemberPosition.Chairman);
        committee.AddMember("m2", "Bob", CommitteeMemberPosition.Member);

        var meeting = BuildInvitationSentMeetingWithOneItem(committee);
        var appraisalId = meeting.Items.Single(i => i.Kind == MeetingItemKind.Decision).AppraisalId;

        meeting.ReleaseItem(appraisalId, "secretary", DateTime.UtcNow);

        var evt = (MeetingItemReleasedDomainEvent)meeting.DomainEvents
            .Single(e => e is MeetingItemReleasedDomainEvent);
        evt.MemberUserIds.Should().BeEquivalentTo(["m1", "m2"]);
    }

    [Fact]
    public void ReleaseItem_WhenAlreadyReleased_Throws()
    {
        // Build a 2-item meeting so releasing item1 doesn't end the meeting
        var meeting = BuildNewMeeting();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        meeting.AddItem(id1, "APR-001", 500_000m, Guid.NewGuid(), "act-1", DateTime.UtcNow);
        meeting.AddItem(id2, "APR-002", 600_000m, Guid.NewGuid(), "act-2", DateTime.UtcNow);
        meeting.SendInvitation(DateTime.UtcNow.AddDays(-1));
        meeting.ClearDomainEvents();
        meeting.ReleaseItem(id1, "secretary", DateTime.UtcNow);

        var act = () => meeting.ReleaseItem(id1, "secretary", DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>().WithMessage("*already*");
    }

    [Fact]
    public void ReleaseItem_OnAcknowledgementItem_Throws()
    {
        var ackMeeting = BuildInvitationSentMeetingWithAckItem();
        var ackAppraisalId = ackMeeting.Items.Single(i => i.Kind == MeetingItemKind.Acknowledgement).AppraisalId;

        var act = () => ackMeeting.ReleaseItem(ackAppraisalId, "secretary", DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>().WithMessage("*acknowledgement*");
    }

    // =========================================================================
    // RouteBackItem
    // =========================================================================

    [Fact]
    public void RouteBackItem_InInvitationSent_TransitionsToRoutedBack()
    {
        var meeting = BuildInvitationSentMeetingWithOneItem();
        var appraisalId = meeting.Items.Single(i => i.Kind == MeetingItemKind.Decision).AppraisalId;

        meeting.RouteBackItem(appraisalId, "secretary", "needs correction", DateTime.UtcNow);

        meeting.Status.Should().Be(MeetingStatus.RoutedBack);
        var item = meeting.Items.Single(i => i.AppraisalId == appraisalId);
        item.ItemDecision.Should().Be(ItemDecision.RoutedBack);
        item.DecisionReason.Should().Be("needs correction");
    }

    [Fact]
    public void RouteBackItem_Raises_MeetingItemRoutedBackDomainEvent()
    {
        var meeting = BuildInvitationSentMeetingWithOneItem();
        var appraisalId = meeting.Items.Single(i => i.Kind == MeetingItemKind.Decision).AppraisalId;
        meeting.ClearDomainEvents();

        meeting.RouteBackItem(appraisalId, "secretary", "needs correction", DateTime.UtcNow);

        var evt = (MeetingItemRoutedBackDomainEvent)meeting.DomainEvents
            .Single(e => e is MeetingItemRoutedBackDomainEvent);
        evt.Reason.Should().Be("needs correction");
    }

    [Fact]
    public void RouteBackItem_EmptyReason_Throws()
    {
        var meeting = BuildInvitationSentMeetingWithOneItem();
        var appraisalId = meeting.Items.Single(i => i.Kind == MeetingItemKind.Decision).AppraisalId;

        var act = () => meeting.RouteBackItem(appraisalId, "secretary", "", DateTime.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RouteBackItem_InNew_Throws()
    {
        var meeting = BuildNewMeeting();
        var appraisalId = Guid.NewGuid();
        meeting.AddItem(appraisalId, "APR-001", 500_000m, Guid.NewGuid(), "act-1", DateTime.UtcNow);

        var act = () => meeting.RouteBackItem(appraisalId, "secretary", "reason", DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>().WithMessage("*InvitationSent or RoutedBack*");
    }

    [Fact]
    public void RouteBackItem_InRoutedBack_StaysRoutedBack()
    {
        var meeting = BuildNewMeeting();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        meeting.AddItem(id1, "APR-001", 500_000m, Guid.NewGuid(), "act-1", DateTime.UtcNow);
        meeting.AddItem(id2, "APR-002", 600_000m, Guid.NewGuid(), "act-2", DateTime.UtcNow);
        meeting.SendInvitation(DateTime.UtcNow.AddDays(-1));

        meeting.RouteBackItem(id1, "secretary", "reason1", DateTime.UtcNow);
        meeting.Status.Should().Be(MeetingStatus.RoutedBack);

        meeting.RouteBackItem(id2, "secretary", "reason2", DateTime.UtcNow);
        meeting.Status.Should().Be(MeetingStatus.RoutedBack);
    }

    [Fact]
    public void RouteBackItem_OnAcknowledgementItem_Throws()
    {
        var ackMeeting = BuildInvitationSentMeetingWithAckItem();
        var ackAppraisalId = ackMeeting.Items.Single(i => i.Kind == MeetingItemKind.Acknowledgement).AppraisalId;

        var act = () => ackMeeting.RouteBackItem(ackAppraisalId, "secretary", "bad data", DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>().WithMessage("*acknowledgement*");
    }

    // =========================================================================
    // Cancel
    // =========================================================================

    [Fact]
    public void Cancel_InNew_WithReason_Succeeds()
    {
        var meeting = BuildNewMeeting();

        meeting.Cancel("budget frozen", DateTime.UtcNow);

        meeting.Status.Should().Be(MeetingStatus.Cancelled);
        meeting.CancelReason.Should().Be("budget frozen");
        meeting.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public void Cancel_InInvitationSent_WithReason_Succeeds()
    {
        var meeting = BuildNewMeeting();
        meeting.SendInvitation(DateTime.UtcNow.AddDays(-1));

        meeting.Cancel("venue not available", DateTime.UtcNow);

        meeting.Status.Should().Be(MeetingStatus.Cancelled);
    }

    [Fact]
    public void Cancel_InRoutedBack_Throws()
    {
        var meeting = BuildInvitationSentMeetingWithOneItem();
        var appraisalId = meeting.Items.Single(i => i.Kind == MeetingItemKind.Decision).AppraisalId;
        meeting.RouteBackItem(appraisalId, "secretary", "reason", DateTime.UtcNow);

        var act = () => meeting.Cancel("reason", DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_WhenEnded_Throws()
    {
        var meeting = BuildInvitationSentMeetingWithOneItem();
        var appraisalId = meeting.Items.Single(i => i.Kind == MeetingItemKind.Decision).AppraisalId;
        meeting.ReleaseItem(appraisalId, "secretary", DateTime.UtcNow);

        var act = () => meeting.Cancel("reason", DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_EmptyReason_Throws()
    {
        var meeting = BuildNewMeeting();
        var act = () => meeting.Cancel("   ", DateTime.UtcNow);
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Cancel on a meeting with ZERO decision items must still raise exactly one
    /// MeetingCancelledDomainEvent so the handler can detach ack items.
    /// </summary>
    [Fact]
    public void Cancel_ZeroDecisionItems_RaisesSingleCancelEvent()
    {
        var meeting = BuildNewMeeting();
        var ackItem = BuildAckItem();
        meeting.CutOff([], new[] { ackItem }, DateTime.UtcNow);
        meeting.ClearDomainEvents();

        meeting.Cancel("cancelled before invitation", DateTime.UtcNow);

        var events = meeting.DomainEvents.OfType<MeetingCancelledDomainEvent>().ToList();
        events.Should().HaveCount(1);
        var evt = events.Single();
        evt.Reason.Should().Be("cancelled before invitation");
        evt.DecisionItems.Should().BeEmpty();
    }

    // =========================================================================
    // AddMember / RemoveMember / ChangeMemberPosition
    // =========================================================================

    [Fact]
    public void AddMember_InNew_Succeeds()
    {
        var meeting = BuildNewMeeting();
        var member = MeetingMember.CreateManual(meeting.Id, "user-x", "Xavier", CommitteeMemberPosition.UW);

        meeting.AddMember(member, DateTime.UtcNow);

        meeting.Members.Should().ContainSingle(m => m.UserId == "user-x");
    }

    [Fact]
    public void AddMember_InInvitationSent_Succeeds()
    {
        var meeting = BuildNewMeeting();
        meeting.SendInvitation(DateTime.UtcNow.AddDays(-1));
        var member = MeetingMember.CreateManual(meeting.Id, "user-x", "Xavier", CommitteeMemberPosition.UW);

        meeting.AddMember(member, DateTime.UtcNow);

        meeting.Members.Should().ContainSingle(m => m.UserId == "user-x");
    }

    [Fact]
    public void AddMember_WhenEnded_Throws()
    {
        var meeting = BuildInvitationSentMeetingWithOneItem();
        var appraisalId = meeting.Items.Single(i => i.Kind == MeetingItemKind.Decision).AppraisalId;
        meeting.ReleaseItem(appraisalId, "secretary", DateTime.UtcNow);
        var member = MeetingMember.CreateManual(meeting.Id, "user-y", "Yvonne", CommitteeMemberPosition.Risk);

        var act = () => meeting.AddMember(member, DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddMember_DuplicateUser_Throws()
    {
        var meeting = BuildNewMeeting();
        var member1 = MeetingMember.CreateManual(meeting.Id, "user-dup", "Duper", CommitteeMemberPosition.Member);
        meeting.AddMember(member1, DateTime.UtcNow);
        var member2 = MeetingMember.CreateManual(meeting.Id, "user-dup", "Duper Clone", CommitteeMemberPosition.Credit);

        var act = () => meeting.AddMember(member2, DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>().WithMessage("*already a member*");
    }

    [Fact]
    public void ChangeMemberPosition_InNew_UpdatesPosition()
    {
        var meeting = BuildNewMeeting();
        var member = MeetingMember.CreateManual(meeting.Id, "user-pos", "Posie", CommitteeMemberPosition.Member);
        meeting.AddMember(member, DateTime.UtcNow);

        meeting.ChangeMemberPosition(member.Id, CommitteeMemberPosition.Director, DateTime.UtcNow);

        meeting.Members.Single(m => m.Id == member.Id).Position.Should().Be(CommitteeMemberPosition.Director);
    }

    // =========================================================================
    // Committee.GetActiveMembers(seq) — rotation logic
    // =========================================================================

    [Fact]
    public void Committee_GetActiveMembersWithSeq_OddSeq_IncludesAlwaysAndOdd()
    {
        var committee = BuildCommittee();
        var always = committee.AddMember("u1", "Always", CommitteeMemberPosition.Chairman);
        // always.Attendance is Always by default
        var odd = committee.AddMember("u2", "Odd", CommitteeMemberPosition.Member);
        odd.UpdateAttendance(CommitteeAttendance.Odd);
        var even = committee.AddMember("u3", "Even", CommitteeMemberPosition.Member);
        even.UpdateAttendance(CommitteeAttendance.Even);

        var result = committee.GetActiveMembers(meetingSeq: 1);

        result.Select(m => m.UserId).Should().BeEquivalentTo(["u1", "u2"]);
    }

    [Fact]
    public void Committee_GetActiveMembersWithSeq_EvenSeq_IncludesAlwaysAndEven()
    {
        var committee = BuildCommittee();
        var always = committee.AddMember("u1", "Always", CommitteeMemberPosition.Chairman);
        var odd = committee.AddMember("u2", "Odd", CommitteeMemberPosition.Member);
        odd.UpdateAttendance(CommitteeAttendance.Odd);
        var even = committee.AddMember("u3", "Even", CommitteeMemberPosition.Member);
        even.UpdateAttendance(CommitteeAttendance.Even);

        var result = committee.GetActiveMembers(meetingSeq: 2);

        result.Select(m => m.UserId).Should().BeEquivalentTo(["u1", "u3"]);
    }

    [Fact]
    public void Committee_GetActiveMembersWithSeq_InactiveMembersExcluded()
    {
        var committee = BuildCommittee();
        var active = committee.AddMember("u1", "Active", CommitteeMemberPosition.Chairman);
        var inactive = committee.AddMember("u2", "Inactive", CommitteeMemberPosition.Member);
        inactive.Deactivate();

        var result = committee.GetActiveMembers(meetingSeq: 1);

        result.Select(m => m.UserId).Should().BeEquivalentTo(["u1"]);
    }

    [Fact]
    public void Committee_GetActiveMembersNoArg_ReturnsAllActive_IgnoresAttendance()
    {
        var committee = BuildCommittee();
        var always = committee.AddMember("u1", "Always", CommitteeMemberPosition.Chairman);
        var odd = committee.AddMember("u2", "Odd", CommitteeMemberPosition.Member);
        odd.UpdateAttendance(CommitteeAttendance.Odd);

        // No-arg overload must still return all active members regardless of Attendance
        var result = committee.GetActiveMembers();

        result.Select(m => m.UserId).Should().BeEquivalentTo(["u1", "u2"]);
    }

    // =========================================================================
    // InProgress guard — EnsureNotInProgress
    // =========================================================================

    [Fact]
    public void CutOff_WhenEffectivelyInProgress_Throws()
    {
        var meeting = BuildNewMeeting();
        var startAt = DateTime.UtcNow.AddMinutes(-1);  // started 1 min ago
        var beforeNow = startAt.AddMinutes(-5);
        meeting.SetSchedule(startAt, startAt.AddHours(2), null, beforeNow);
        meeting.SendInvitation(beforeNow);

        var now = DateTime.UtcNow;
        var act = () => meeting.CutOff([], [], now);

        act.Should().Throw<InvalidOperationException>().WithMessage("*started*");
    }

    [Fact]
    public void CutOff_WhenStartAtInFuture_Succeeds()
    {
        var meeting = BuildNewMeeting();
        var now = DateTime.UtcNow;
        var startAt = now.AddHours(1);  // starts in 1 hour — not InProgress
        meeting.SetSchedule(startAt, startAt.AddHours(2), null, now.AddMinutes(-5));
        meeting.SendInvitation(now.AddMinutes(-5));

        // CutOff should succeed because StartAt > now
        meeting.CutOff([], [], now);
    }

    [Fact]
    public void Cancel_WhenEffectivelyInProgress_Throws()
    {
        var meeting = BuildNewMeeting();
        var startAt = DateTime.UtcNow.AddMinutes(-1);
        var beforeNow = startAt.AddMinutes(-5);
        meeting.SetSchedule(startAt, startAt.AddHours(2), null, beforeNow);
        meeting.SendInvitation(beforeNow);

        var now = DateTime.UtcNow;
        var act = () => meeting.Cancel("budget frozen", now);

        act.Should().Throw<InvalidOperationException>().WithMessage("*started*");
    }

    [Fact]
    public void Cancel_WhenStartAtInFuture_Succeeds()
    {
        var meeting = BuildNewMeeting();
        var now = DateTime.UtcNow;
        var startAt = now.AddHours(1);
        meeting.SetSchedule(startAt, startAt.AddHours(2), null, now.AddMinutes(-5));
        meeting.SendInvitation(now.AddMinutes(-5));

        meeting.Cancel("venue change", now);

        meeting.Status.Should().Be(MeetingStatus.Cancelled);
    }

    [Fact]
    public void AddItem_WhenEffectivelyInProgress_Throws()
    {
        var meeting = BuildNewMeeting();
        var startAt = DateTime.UtcNow.AddMinutes(-1);
        var beforeNow = startAt.AddMinutes(-5);
        meeting.SetSchedule(startAt, startAt.AddHours(2), null, beforeNow);
        meeting.SendInvitation(beforeNow);

        var now = DateTime.UtcNow;
        var act = () => meeting.AddItem(Guid.NewGuid(), "APR-001", 100_000m, Guid.NewGuid(), "act", now);

        act.Should().Throw<InvalidOperationException>().WithMessage("*started*");
    }

    [Fact]
    public void SetSchedule_WhenEffectivelyInProgress_Throws()
    {
        var meeting = BuildNewMeeting();
        var startAt = DateTime.UtcNow.AddMinutes(-1);
        var beforeNow = startAt.AddMinutes(-5);
        meeting.SetSchedule(startAt, startAt.AddHours(2), null, beforeNow);
        meeting.SendInvitation(beforeNow);

        var now = DateTime.UtcNow;
        var act = () => meeting.SetSchedule(now.AddHours(2), now.AddHours(4), null, now);

        act.Should().Throw<InvalidOperationException>().WithMessage("*started*");
    }

    // =========================================================================
    // ReinstateRoutedBackItem
    // =========================================================================

    [Fact]
    public void ReinstateRoutedBackItem_WhenPresent_FlipsBackToPending_MeetingStaysRoutedBack()
    {
        var meeting = BuildInvitationSentMeetingWithOneItem();
        var appraisalId = meeting.Items.Single(i => i.Kind == MeetingItemKind.Decision).AppraisalId;
        meeting.RouteBackItem(appraisalId, "secretary", "needs rework", DateTime.UtcNow);
        meeting.ClearDomainEvents();

        meeting.ReinstateRoutedBackItem(appraisalId, DateTime.UtcNow);

        var item = meeting.Items.Single(i => i.Kind == MeetingItemKind.Decision);
        item.ItemDecision.Should().Be(ItemDecision.Pending);
        meeting.Status.Should().Be(MeetingStatus.RoutedBack);
    }

    [Fact]
    public void ReinstateRoutedBackItem_ThenReleaseAll_TransitionsToEnded()
    {
        var meeting = BuildInvitationSentMeetingWithOneItem();
        var appraisalId = meeting.Items.Single(i => i.Kind == MeetingItemKind.Decision).AppraisalId;
        var now = DateTime.UtcNow;
        meeting.RouteBackItem(appraisalId, "secretary", "needs rework", now);

        meeting.ReinstateRoutedBackItem(appraisalId, now.AddMinutes(5));
        meeting.ReleaseItem(appraisalId, "secretary", now.AddMinutes(10));

        meeting.Status.Should().Be(MeetingStatus.Ended);
        meeting.EndedAt.Should().NotBeNull();
    }

    [Fact]
    public void ReinstateRoutedBackItem_WhenItemIsNotRoutedBack_Throws()
    {
        // Item is still Pending — reinstate should throw
        var meeting = BuildInvitationSentMeetingWithOneItem();
        var appraisalId = meeting.Items.Single(i => i.Kind == MeetingItemKind.Decision).AppraisalId;

        var act = () => meeting.ReinstateRoutedBackItem(appraisalId, DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>().WithMessage("*RoutedBack*");
    }

    [Fact]
    public void ReinstateRoutedBackItem_WhenMeetingNotRoutedBack_Throws()
    {
        // Meeting is still InvitationSent (no item routed back yet)
        var meeting = BuildInvitationSentMeetingWithOneItem();
        var appraisalId = meeting.Items.Single(i => i.Kind == MeetingItemKind.Decision).AppraisalId;

        var act = () => meeting.ReinstateRoutedBackItem(appraisalId, DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>().WithMessage("*RoutedBack*");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static Meeting BuildNewMeeting(string meetingNo = "1/2568", int seq = 1, int year = 2568)
        => Meeting.Create("Test Meeting", null, meetingNo, seq, year);

    private static Committee BuildCommittee()
        => Committee.Create("Test Committee", "TC", null, QuorumType.Fixed, 2, MajorityType.Simple);

    /// <summary>
    /// Builds an InvitationSent meeting with one Decision item.
    /// </summary>
    private static Meeting BuildInvitationSentMeetingWithOneItem(Committee? committee = null)
    {
        var meeting = BuildNewMeeting();

        if (committee is not null)
            meeting.SnapshotCommittee(committee, meetingSeq: 1);

        var appraisalId = Guid.NewGuid();
        meeting.AddItem(appraisalId, "APR-001", 500_000m, Guid.NewGuid(), "meeting-act", DateTime.UtcNow);

        meeting.SendInvitation(DateTime.UtcNow.AddDays(-1));
        meeting.ClearDomainEvents();

        return meeting;
    }

    /// <summary>
    /// Builds an InvitationSent meeting that contains one Acknowledgement item AND one Decision item.
    /// </summary>
    private static Meeting BuildInvitationSentMeetingWithAckItem()
    {
        var meeting = BuildNewMeeting();

        // Add a decision item (needed so release tests have something to work with)
        meeting.AddItem(Guid.NewGuid(), "APR-DEC-001", 100_000m, Guid.NewGuid(), "act-dec", DateTime.UtcNow);

        // CutOff adds an ack item
        var ackItem = BuildAckItem();
        meeting.CutOff([], new[] { ackItem }, DateTime.UtcNow);

        meeting.SendInvitation(DateTime.UtcNow.AddDays(-1));
        meeting.ClearDomainEvents();

        return meeting;
    }

    private static MeetingQueueItem BuildQueueItem(Guid? appraisalId = null)
        => MeetingQueueItem.CreateQueued(
            appraisalId ?? Guid.NewGuid(),
            "APR-Q-001",
            800_000m,
            Guid.NewGuid(),
            "queue-act");

    private static AppraisalAcknowledgementQueueItem BuildAckItem(Guid? appraisalId = null)
        => AppraisalAcknowledgementQueueItem.Create(
            appraisalId ?? Guid.NewGuid(),
            "APR-ACK-001",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "G1",
            "Group1",
            DateTime.UtcNow);
}
