using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetPreviousAppraisalChain;

public record GetPreviousAppraisalChainQuery(Guid AppraisalId) : IQuery<GetPreviousAppraisalChainResult>;
