using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetLawAndRegulations;

public record GetLawAndRegulationsQuery(
    Guid AppraisalId
) : IQuery<GetLawAndRegulationsResult>;
