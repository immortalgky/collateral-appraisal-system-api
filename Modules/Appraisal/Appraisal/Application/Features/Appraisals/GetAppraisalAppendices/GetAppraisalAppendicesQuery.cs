using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalAppendices;

public record GetAppraisalAppendicesQuery(
    Guid AppraisalId
) : IQuery<GetAppraisalAppendicesResult>;
