using Shared.DDD;

namespace Workflow.Meetings.ReadModels;

public enum AcknowledgementStatus
{
    /// <summary>Waiting to be included in a meeting for acknowledgement.</summary>
    PendingAcknowledgement,
    /// <summary>Included in a meeting's cut-off snapshot; awaiting end of meeting.</summary>
    Included,
    /// <summary>Meeting ended and the acknowledgement is complete.</summary>
    Acknowledged
}

/// <summary>
/// Read-model entity representing an appraisal that was approved by a sub-committee or
/// Group-1 committee without a full meeting and must be acknowledged at the next meeting.
/// </summary>
public class AppraisalAcknowledgementQueueItem : Entity<Guid>
{
    public Guid AppraisalId { get; private set; }
    public string? AppraisalNo { get; private set; }
    public Guid AppraisalDecisionId { get; private set; }
    public Guid CommitteeId { get; private set; }
    public string CommitteeCode { get; private set; } = default!;

    /// <summary>
    /// Logical grouping used to bucket this item in the meeting agenda
    /// (e.g. "Group1" or "UrgentGroup2").
    /// </summary>
    public string AcknowledgementGroup { get; private set; } = default!;
    public AcknowledgementStatus Status { get; private set; }
    public Guid? MeetingId { get; private set; }
    public DateTime EnqueuedAt { get; private set; }

    private AppraisalAcknowledgementQueueItem() { }

    public static AppraisalAcknowledgementQueueItem Create(
        Guid appraisalId,
        string? appraisalNo,
        Guid appraisalDecisionId,
        Guid committeeId,
        string committeeCode,
        string acknowledgementGroup)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(committeeCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(acknowledgementGroup);

        return new AppraisalAcknowledgementQueueItem
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId,
            AppraisalNo = appraisalNo,
            AppraisalDecisionId = appraisalDecisionId,
            CommitteeId = committeeId,
            CommitteeCode = committeeCode,
            AcknowledgementGroup = acknowledgementGroup,
            Status = AcknowledgementStatus.PendingAcknowledgement,
            EnqueuedAt = DateTime.Now
        };
    }

    /// <summary>Includes this item in a meeting cut-off snapshot.</summary>
    public void Include(Guid meetingId)
    {
        if (Status != AcknowledgementStatus.PendingAcknowledgement)
            throw new InvalidOperationException(
                $"Cannot include acknowledgement item in status {Status}");
        MeetingId = meetingId;
        Status = AcknowledgementStatus.Included;
    }

    /// <summary>Marks this item as acknowledged after the meeting ends.</summary>
    public void Acknowledge()
    {
        if (Status != AcknowledgementStatus.Included)
            throw new InvalidOperationException(
                $"Cannot acknowledge item in status {Status}");
        Status = AcknowledgementStatus.Acknowledged;
    }

    /// <summary>Returns this item to pending when its meeting is cancelled.</summary>
    public void ReturnToPending()
    {
        if (Status != AcknowledgementStatus.Included)
            throw new InvalidOperationException(
                $"Cannot return to pending from status {Status}");
        MeetingId = null;
        Status = AcknowledgementStatus.PendingAcknowledgement;
    }
}
