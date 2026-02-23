namespace Appraisal.Domain.Committees;

/// <summary>
/// Individual vote record for a committee review.
/// </summary>
public class CommitteeVote : Entity<Guid>
{
    public Guid ReviewId { get; private set; }
    public Guid CommitteeMemberId { get; private set; }
    public string MemberName { get; private set; } = null!;
    public string MemberRole { get; private set; } = null!;
    public string Vote { get; private set; } = null!; // Approve, Reject, Abstain
    public DateTime VotedAt { get; private set; }
    public string? Comments { get; private set; }

    private CommitteeVote()
    {
    }

    public static CommitteeVote Create(
        Guid reviewId,
        Guid committeeMemberId,
        string memberName,
        string memberRole,
        string vote,
        string? comments = null)
    {
        ValidateVote(vote);

        return new CommitteeVote
        {
            Id = Guid.CreateVersion7(),
            ReviewId = reviewId,
            CommitteeMemberId = committeeMemberId,
            MemberName = memberName,
            MemberRole = memberRole,
            Vote = vote,
            VotedAt = DateTime.UtcNow,
            Comments = comments
        };
    }

    private static void ValidateVote(string vote)
    {
        var validVotes = new[] { "Approve", "Reject", "Abstain" };
        if (!validVotes.Contains(vote))
            throw new ArgumentException($"Invalid vote: {vote}");
    }
}