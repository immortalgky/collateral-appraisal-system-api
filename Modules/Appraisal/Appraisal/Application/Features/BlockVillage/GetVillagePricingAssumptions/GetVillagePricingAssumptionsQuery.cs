namespace Appraisal.Application.Features.BlockVillage.GetVillagePricingAssumptions;

public record GetVillagePricingAssumptionsQuery(Guid AppraisalId) : IQuery<GetVillagePricingAssumptionsResult>;
