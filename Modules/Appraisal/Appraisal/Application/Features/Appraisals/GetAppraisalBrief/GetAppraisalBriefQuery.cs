using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalBrief;

public record GetAppraisalBriefQuery(Guid AppraisalId) : IQuery<GetAppraisalBriefResult>;
