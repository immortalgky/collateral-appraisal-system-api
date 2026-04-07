namespace Appraisal.Application.Features.BlockCondo.GetCondoPricingAssumptions;

public record GetCondoPricingAssumptionsQuery(Guid AppraisalId) : IQuery<GetCondoPricingAssumptionsResult>;
