namespace Appraisal.Application.Features.CommitteeVoting.SubmitVote;

public record SubmitVoteResult(
    Guid VoteId,
    string ReviewStatus,
    bool IsAutoApproved
);
