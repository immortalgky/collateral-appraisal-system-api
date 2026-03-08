namespace Appraisal.Application.Features.CommitteeVoting.SubmitVote;

public record SubmitVoteResponse(
    Guid VoteId,
    string ReviewStatus,
    bool IsAutoApproved
);
