namespace Appraisal.Application.Features.Fees.GetAppraisalFees;

public record GetAppraisalFeesQuery(Guid AppraisalId) : IQuery<GetAppraisalFeesResult>;
