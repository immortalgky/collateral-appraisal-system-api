namespace Appraisal.Application.Features.CommitteeVoting.GetApprovalList;

public record GetApprovalListQuery(Guid AppraisalId) : IQuery<GetApprovalListResult>;
