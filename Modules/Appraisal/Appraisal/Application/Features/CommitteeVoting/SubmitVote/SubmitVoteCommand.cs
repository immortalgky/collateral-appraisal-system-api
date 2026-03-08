namespace Appraisal.Application.Features.CommitteeVoting.SubmitVote;

public record SubmitVoteCommand(
    Guid AppraisalId,
    Guid ReviewId,
    string Vote,
    string? Remark
) : ICommand<SubmitVoteResult>;
