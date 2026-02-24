using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalComparables;

public record GetAppraisalComparablesQuery(Guid AppraisalId) : IQuery<GetAppraisalComparablesResult>;
