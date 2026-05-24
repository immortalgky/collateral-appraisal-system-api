namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Per-appraisal committee approval <b>outcome</b> record (one row per appraisal, created at
/// committee approval). Approval tier (1/2/3) is NOT stored — it is derived in the read views
/// from the committee code (via <see cref="CommitteeId"/>). Meeting number/date are likewise
/// resolved by joining the meeting on <see cref="MeetingId"/>.
/// </summary>
public class AppraisalReview : Entity<Guid>
{
    public Guid AppraisalId { get; private set; }

    /// <summary>The committee that approved. Tier is derived from its code in read views.</summary>
    public Guid? CommitteeId { get; private set; }

    public int? TotalVotes { get; private set; }
    public int? VotesApprove { get; private set; }
    public int? VotesReject { get; private set; }

    /// <summary>Holds route-back votes (this domain has no "abstain"); kept for the spare tally slot.</summary>
    public int? VotesAbstain { get; private set; }

    /// <summary>
    /// The meeting this review is linked to: the decision meeting for committee-with-meeting
    /// approvals, or the acknowledgement meeting for sub-committee / committee approvals.
    /// </summary>
    public Guid? MeetingId { get; private set; }

    public DateTime? ReviewedAt { get; private set; }

    private AppraisalReview()
    {
    }

    public static AppraisalReview Create(Guid appraisalId)
    {
        return new AppraisalReview
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId
        };
    }

    public void RecordVotes(int approve, int reject, int abstain)
    {
        VotesApprove = approve;
        VotesReject = reject;
        VotesAbstain = abstain;
        TotalVotes = approve + reject + abstain;
    }

    /// <summary>
    /// Records a committee approval outcome (committee, vote totals, optional decision meeting).
    /// Used by the approval integration-event consumer.
    /// </summary>
    public void RecordCommitteeApproval(
        Guid? committeeId,
        int approve,
        int reject,
        int abstain,
        DateTime approvedAt,
        Guid? decisionMeetingId)
    {
        CommitteeId = committeeId;
        RecordVotes(approve, reject, abstain);
        ReviewedAt = approvedAt;

        if (decisionMeetingId is { } meetingId && meetingId != Guid.Empty)
            MeetingId = meetingId;
    }

    /// <summary>
    /// Links this review to the meeting in which a sub-committee / committee approval was acknowledged.
    /// </summary>
    public void SetAcknowledgementMeeting(Guid meetingId)
    {
        MeetingId = meetingId;
    }
}
