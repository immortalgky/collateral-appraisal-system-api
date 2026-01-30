namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Appraisal review entity.
/// Tracks review workflow: Checker -> Verifier -> Committee
/// </summary>
public class AppraisalReview : Entity<Guid>
{
    // Core Properties
    public Guid AppraisalId { get; private set; }
    public string ReviewLevel { get; private set; } = null!; // Checker, Verifier, Committee
    public int ReviewSequence { get; private set; }
    public ReviewStatus Status { get; private set; } = null!;

    // Reviewer
    public Guid? AssignedTo { get; private set; }
    public DateTime? AssignedAt { get; private set; }
    public Guid? AssignedBy { get; private set; }

    // Team (Checker -> Verifier same team)
    public Guid? TeamId { get; private set; }
    public string? TeamName { get; private set; }

    // Committee (if Committee level)
    public Guid? CommitteeId { get; private set; }
    public int? TotalVotes { get; private set; }
    public int? VotesApprove { get; private set; }
    public int? VotesReject { get; private set; }
    public int? VotesAbstain { get; private set; }
    public DateTime? MeetingDate { get; private set; }
    public string? MeetingReference { get; private set; }

    // Review Result
    public DateTime? ReviewedAt { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public string? ReviewComments { get; private set; }
    public string? ReturnReason { get; private set; }

    private AppraisalReview()
    {
    }

    public static AppraisalReview Create(
        Guid appraisalId,
        string reviewLevel,
        int reviewSequence)
    {
        return new AppraisalReview
        {
            Id = Guid.NewGuid(),
            AppraisalId = appraisalId,
            ReviewLevel = reviewLevel,
            ReviewSequence = reviewSequence,
            Status = ReviewStatus.Pending
        };
    }

    public void AssignTo(Guid userId, Guid assignedBy, Guid? teamId = null, string? teamName = null)
    {
        AssignedTo = userId;
        AssignedAt = DateTime.UtcNow;
        AssignedBy = assignedBy;
        TeamId = teamId;
        TeamName = teamName;
    }

    public void SetCommittee(Guid committeeId)
    {
        if (ReviewLevel != "Committee")
            throw new InvalidOperationException("Can only set committee for Committee level reviews");

        CommitteeId = committeeId;
    }

    public void Approve(Guid reviewedBy, string? comments = null)
    {
        Status = ReviewStatus.Approved;
        ReviewedAt = DateTime.UtcNow;
        ReviewedBy = reviewedBy;
        ReviewComments = comments;
    }

    public void Return(Guid reviewedBy, string reason, string? comments = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Return reason is required");

        Status = ReviewStatus.Returned;
        ReviewedAt = DateTime.UtcNow;
        ReviewedBy = reviewedBy;
        ReturnReason = reason;
        ReviewComments = comments;
    }

    public void RecordVotes(int approve, int reject, int abstain)
    {
        VotesApprove = approve;
        VotesReject = reject;
        VotesAbstain = abstain;
        TotalVotes = approve + reject + abstain;
    }

    public void SetMeetingInfo(DateTime meetingDate, string? reference)
    {
        MeetingDate = meetingDate;
        MeetingReference = reference;
    }
}