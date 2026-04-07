namespace Appraisal.Application.Features.BlockVillage.GetVillageUnitPrices;

public record GetVillageUnitPricesQuery(Guid AppraisalId) : IQuery<GetVillageUnitPricesResult>;
