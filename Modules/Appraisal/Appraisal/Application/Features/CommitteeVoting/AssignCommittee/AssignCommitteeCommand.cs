namespace Appraisal.Application.Features.CommitteeVoting.AssignCommittee;

public record AssignCommitteeCommand(Guid AppraisalId) : ICommand<AssignCommitteeResult>;
