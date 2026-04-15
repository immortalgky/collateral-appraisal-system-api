using Shared.DDD;
using Workflow.Domain.Committees;
using Workflow.Meetings.Domain.Events;
using Workflow.Meetings.ReadModels;

namespace Workflow.Meetings.Domain;

public class Meeting : Aggregate<Guid>
{
    public string Title { get; private set; } = default!;
    public string? Location { get; private set; }
    public string? Notes { get; private set; }
    public MeetingStatus Status { get; private set; }
    public string? CancelReason { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    // ----- New fields (Phase 1) -----

    public string? MeetingNo { get; private set; }
    public int? MeetingNoYear { get; private set; }
    public int? MeetingNoSeq { get; private set; }

    /// <summary>Scheduled start time; replaces the old <c>ScheduledAt</c> field (see migration).</summary>
    public DateTime? StartAt { get; private set; }

    /// <summary>Scheduled end time.</summary>
    public DateTime? EndAt { get; private set; }

    /// <summary>Free-text "from" field for the meeting agenda header.</summary>
    public string? FromText { get; private set; }

    /// <summary>Free-text "to" field for the meeting agenda header.</summary>
    public string? ToText { get; private set; }

    public string? AgendaCertifyMinutes { get; private set; }
    public string? AgendaChairmanInformed { get; private set; }
    public string? AgendaOthers { get; private set; }

    public DateTime? CutOffAt { get; private set; }
    public DateTime? InvitationSentAt { get; private set; }

    /// <summary>Optimistic concurrency token managed by EF Core.</summary>
    public byte[] RowVersion { get; private set; } = default!;

    // ----- Collections -----

    private readonly List<MeetingItem> _items = new();
    public IReadOnlyList<MeetingItem> Items => _items.AsReadOnly();

    private readonly List<MeetingMember> _members = new();
    public IReadOnlyList<MeetingMember> Members => _members.AsReadOnly();

    private Meeting() { }

    // -------------------------------------------------------------------------
    // Factory
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates a Draft meeting.
    /// Design choice: <see cref="SnapshotCommittee"/> is a separate call rather than being
    /// embedded in Create, because the application handler loads the committee from its own
    /// repository and passes it in. Keeping them separate avoids a cross-aggregate dependency
    /// inside the factory and makes bulk-create (dates only, no committee snapshot yet) simpler.
    /// </summary>
    public static Meeting Create(string title, string? notes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        return new Meeting
        {
            Id = Guid.CreateVersion7(),
            Title = title.Trim(),
            Notes = notes,
            Status = MeetingStatus.Draft
        };
    }

    // -------------------------------------------------------------------------
    // Member management
    // -------------------------------------------------------------------------

    /// <summary>
    /// Copies all active members of <paramref name="committee"/> into this meeting.
    /// May only be called once (throws if members already exist).
    /// </summary>
    public void SnapshotCommittee(Committee committee)
    {
        ArgumentNullException.ThrowIfNull(committee);

        if (_members.Count > 0)
            throw new InvalidOperationException(
                "Committee snapshot has already been taken for this meeting");

        foreach (var cm in committee.GetActiveMembers())
            _members.Add(MeetingMember.CreateSnapshot(Id, cm));
    }

    public void AddMember(MeetingMember member)
    {
        EnsureMutableStatus();
        ArgumentNullException.ThrowIfNull(member);

        if (_members.Any(m => m.UserId == member.UserId))
            throw new InvalidOperationException(
                $"User {member.UserId} is already a member of this meeting");

        _members.Add(member);
    }

    public void RemoveMember(Guid memberId)
    {
        EnsureMutableStatus();

        var member = _members.FirstOrDefault(m => m.Id == memberId)
            ?? throw new InvalidOperationException(
                $"Meeting member {memberId} not found");

        _members.Remove(member);
    }

    public void ChangeMemberPosition(Guid memberId, CommitteeMemberPosition position)
    {
        EnsureMutableStatus();

        var member = _members.FirstOrDefault(m => m.Id == memberId)
            ?? throw new InvalidOperationException(
                $"Meeting member {memberId} not found");

        member.UpdatePosition(position);
    }

    // -------------------------------------------------------------------------
    // Item management (existing — kept for compatibility with Phase 5 refactor)
    // -------------------------------------------------------------------------

    public MeetingItem AddItem(
        Guid appraisalId,
        string? appraisalNo,
        decimal facilityLimit,
        Guid workflowInstanceId,
        string activityId)
    {
        if (Status != MeetingStatus.Draft && Status != MeetingStatus.Scheduled)
            throw new InvalidOperationException(
                $"Cannot add items to a meeting in status {Status}");

        if (_items.Any(i => i.AppraisalId == appraisalId))
            throw new InvalidOperationException(
                $"Appraisal {appraisalId} is already on this meeting");

        var item = MeetingItem.CreateDecision(
            Id, appraisalId, appraisalNo, facilityLimit,
            appraisalType: null, workflowInstanceId, activityId);
        _items.Add(item);
        return item;
    }

    public MeetingItem RemoveItem(Guid appraisalId)
    {
        if (Status != MeetingStatus.Draft && Status != MeetingStatus.Scheduled)
            throw new InvalidOperationException(
                $"Cannot remove items from a meeting in status {Status}");

        var item = _items.FirstOrDefault(i => i.AppraisalId == appraisalId)
            ?? throw new InvalidOperationException(
                $"Appraisal {appraisalId} is not on this meeting");

        _items.Remove(item);
        return item;
    }

    // -------------------------------------------------------------------------
    // New methods (Phase 1)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Freezes the meeting agenda by converting queued decision items and pending
    /// acknowledgement items into <see cref="MeetingItem"/> rows.
    /// Only allowed in Draft; idempotency guard: throws if already cut off.
    /// </summary>
    public void CutOff(
        IEnumerable<MeetingQueueItem> queuedItems,
        IEnumerable<AppraisalAcknowledgementQueueItem> ackPendingItems,
        DateTime now)
    {
        if (Status != MeetingStatus.Draft)
            throw new InvalidOperationException(
                $"Cut-off is only allowed in Draft status; current status is {Status}");

        if (CutOffAt.HasValue)
            throw new InvalidOperationException(
                "Meeting has already been cut off");

        var includedAppraisalIds = new List<Guid>();

        foreach (var qi in queuedItems)
        {
            if (_items.Any(i => i.AppraisalId == qi.AppraisalId))
                continue;

            var item = MeetingItem.CreateDecision(
                Id, qi.AppraisalId, qi.AppraisalNo, qi.FacilityLimit,
                appraisalType: null, qi.WorkflowInstanceId, qi.ActivityId);
            _items.Add(item);
            qi.AssignTo(Id);
            includedAppraisalIds.Add(qi.AppraisalId);
        }

        foreach (var ai in ackPendingItems)
        {
            if (_items.Any(i => i.AppraisalId == ai.AppraisalId))
                continue;

            var item = MeetingItem.CreateAcknowledgement(
                Id, ai.AppraisalId, ai.AppraisalNo, facilityLimit: 0,
                appraisalType: null, ai.AcknowledgementGroup, ai.AppraisalDecisionId);
            _items.Add(item);
            ai.Include(Id);
            includedAppraisalIds.Add(ai.AppraisalId);
        }

        CutOffAt = now;

        AddDomainEvent(new MeetingCutOffDomainEvent(Id, includedAppraisalIds.AsReadOnly()));
    }

    /// <summary>
    /// Assigns a <see cref="MeetingNo"/> and transitions Draft→Scheduled.
    /// If already Scheduled (re-send), updates <see cref="InvitationSentAt"/> only — no new MeetingNo, no event.
    /// </summary>
    public async Task SendInvitation(IMeetingNoGenerator generator, DateTime now, CancellationToken ct)
    {
        if (Status == MeetingStatus.Ended || Status == MeetingStatus.Cancelled)
            throw new InvalidOperationException(
                $"Cannot send invitation for a meeting in terminal status {Status}");

        if (Status == MeetingStatus.Scheduled)
        {
            // Idempotent re-send: just update the timestamp
            InvitationSentAt = now;
            return;
        }

        // Draft → Scheduled
        var meetingNo = await generator.NextAsync(now, ct);

        // Parse year and seq from "{seq}/{BE-year}" for storage
        var parts = meetingNo.Split('/');
        if (parts.Length == 2
            && int.TryParse(parts[0], out var seq)
            && int.TryParse(parts[1], out var beYear))
        {
            MeetingNoSeq = seq;
            MeetingNoYear = beYear;
        }

        MeetingNo = meetingNo;
        InvitationSentAt = now;
        Status = MeetingStatus.Scheduled;

        AddDomainEvent(new MeetingInvitationSentDomainEvent(Id, meetingNo));
    }

    public void SetSchedule(DateTime startAt, DateTime endAt, string? location)
    {
        if (Status != MeetingStatus.Draft && Status != MeetingStatus.Scheduled)
            throw new InvalidOperationException(
                $"Cannot set schedule in status {Status}");

        if (endAt <= startAt)
            throw new ArgumentException("EndAt must be after StartAt");

        StartAt = startAt;
        EndAt = endAt;

        if (!string.IsNullOrWhiteSpace(location))
            Location = location;
    }

    public void SetAgenda(
        string? from,
        string? to,
        string? certifyMinutes,
        string? chairmanInformed,
        string? others)
    {
        if (Status != MeetingStatus.Draft && Status != MeetingStatus.Scheduled)
            throw new InvalidOperationException(
                $"Cannot set agenda in status {Status}");

        if (from is not null && from.Length > 200)
            throw new ArgumentException("FromText must not exceed 200 characters");
        if (to is not null && to.Length > 200)
            throw new ArgumentException("ToText must not exceed 200 characters");
        if (certifyMinutes is not null && certifyMinutes.Length > 2000)
            throw new ArgumentException("AgendaCertifyMinutes must not exceed 2000 characters");
        if (chairmanInformed is not null && chairmanInformed.Length > 2000)
            throw new ArgumentException("AgendaChairmanInformed must not exceed 2000 characters");
        if (others is not null && others.Length > 2000)
            throw new ArgumentException("AgendaOthers must not exceed 2000 characters");

        FromText = from;
        ToText = to;
        AgendaCertifyMinutes = certifyMinutes;
        AgendaChairmanInformed = chairmanInformed;
        AgendaOthers = others;
    }

    // -------------------------------------------------------------------------
    // Existing methods (updated)
    // -------------------------------------------------------------------------

    public void UpdateDetails(string title, string? location, string? notes)
    {
        EnsureNotTerminal();
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        Title = title.Trim();
        Location = location;
        Notes = notes;
    }

    /// <summary>
    /// Ends a Scheduled meeting. Items with <see cref="MeetingItemKind.Decision"/> are
    /// flipped to <see cref="ItemDecision.Pending"/> (secretary will act per item).
    /// Raises the reduced <see cref="MeetingEndedDomainEvent"/>.
    /// </summary>
    /// <param name="now">The current UTC time, supplied by the caller to keep the aggregate clock-free.</param>
    public void End(DateTime now)
    {
        if (Status != MeetingStatus.Scheduled)
            throw new InvalidOperationException(
                $"Cannot end a meeting in status {Status}. Meeting must be Scheduled");

        if (_items.Count == 0)
            throw new InvalidOperationException(
                "Cannot end a meeting with no items");

        Status = MeetingStatus.Ended;
        EndedAt = now;

        // Ensure all Decision items have Pending decision so secretary can act
        foreach (var item in _items.Where(i => i.Kind == MeetingItemKind.Decision))
        {
            if (item.ItemDecision != ItemDecision.Pending)
                item.ApplyDecision(ItemDecision.Pending, actor: string.Empty, reason: null, now);
        }

        AddDomainEvent(new MeetingEndedDomainEvent(Id, EndedAt.Value));
    }

    /// <summary>
    /// Cancels a Draft or Scheduled meeting. <paramref name="reason"/> is required.
    /// Raises exactly one <see cref="MeetingCancelledDomainEvent"/> regardless of the number of
    /// decision items (including zero), so the handler always runs to detach ack items.
    /// </summary>
    public void Cancel(string reason)
    {
        if (Status != MeetingStatus.Draft && Status != MeetingStatus.Scheduled)
            throw new InvalidOperationException(
                $"Cannot cancel a meeting in status {Status}");

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        Status = MeetingStatus.Cancelled;
        CancelReason = reason;
        CancelledAt = DateTime.UtcNow;

        var cancelledDecisionItems = _items
            .Where(i => i.Kind == MeetingItemKind.Decision)
            .Select(i => new CancelledDecisionItem(
                i.AppraisalId, i.WorkflowInstanceId!.Value, i.ActivityId!))
            .ToList()
            .AsReadOnly();

        AddDomainEvent(new MeetingCancelledDomainEvent(
            Id, reason, CancelledAt.Value, cancelledDecisionItems));
    }

    /// <summary>
    /// Secretary releases a Decision item after the meeting has ended.
    /// Routes the appraisal to all meeting members as parallel approvers.
    /// </summary>
    public void ReleaseItem(Guid appraisalId, string actor, DateTime now)
    {
        EnsureEnded();
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);

        var item = GetDecisionItemOrThrow(appraisalId);

        if (item.ItemDecision != ItemDecision.Pending)
            throw new InvalidOperationException(
                $"Cannot release item for appraisal {appraisalId}: decision is already {item.ItemDecision}");

        item.ApplyDecision(ItemDecision.Released, actor, reason: null, now);

        // Decision items always have WorkflowInstanceId/ActivityId set — guard defensively.
        if (!item.WorkflowInstanceId.HasValue || item.ActivityId is null)
            throw new InvalidOperationException(
                $"Decision item for appraisal {appraisalId} is missing WorkflowInstanceId or ActivityId");

        var memberUserIds = _members.Select(m => m.UserId).ToList().AsReadOnly();

        AddDomainEvent(new MeetingItemReleasedDomainEvent(
            Id, appraisalId, item.WorkflowInstanceId.Value, item.ActivityId, actor, memberUserIds));
    }

    /// <summary>
    /// Secretary routes a Decision item back to the appraisal team after the meeting has ended.
    /// </summary>
    public void RouteBackItem(Guid appraisalId, string actor, string reason, DateTime now)
    {
        EnsureEnded();
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var item = GetDecisionItemOrThrow(appraisalId);

        if (item.ItemDecision != ItemDecision.Pending)
            throw new InvalidOperationException(
                $"Cannot route back item for appraisal {appraisalId}: decision is already {item.ItemDecision}");

        item.ApplyDecision(ItemDecision.RoutedBack, actor, reason, now);

        // Decision items always have WorkflowInstanceId/ActivityId set — guard defensively.
        if (!item.WorkflowInstanceId.HasValue || item.ActivityId is null)
            throw new InvalidOperationException(
                $"Decision item for appraisal {appraisalId} is missing WorkflowInstanceId or ActivityId");

        AddDomainEvent(new MeetingItemRoutedBackDomainEvent(
            Id, appraisalId, item.WorkflowInstanceId.Value, item.ActivityId, reason, actor));
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void EnsureNotTerminal()
    {
        if (Status == MeetingStatus.Ended || Status == MeetingStatus.Cancelled)
            throw new InvalidOperationException(
                $"Meeting is in terminal status {Status}");
    }

    private void EnsureMutableStatus()
    {
        if (Status != MeetingStatus.Draft && Status != MeetingStatus.Scheduled)
            throw new InvalidOperationException(
                $"This operation is only allowed in Draft or Scheduled status; current status is {Status}");
    }

    private void EnsureEnded()
    {
        if (Status != MeetingStatus.Ended)
            throw new InvalidOperationException(
                $"This operation is only allowed after the meeting has ended; current status is {Status}");
    }

    private MeetingItem GetDecisionItemOrThrow(Guid appraisalId)
    {
        var item = _items.FirstOrDefault(i => i.AppraisalId == appraisalId)
            ?? throw new InvalidOperationException(
                $"Appraisal {appraisalId} is not on this meeting");

        if (item.Kind != MeetingItemKind.Decision)
            throw new InvalidOperationException(
                $"Appraisal {appraisalId} is an acknowledgement item and cannot be released or routed back");

        return item;
    }
}
