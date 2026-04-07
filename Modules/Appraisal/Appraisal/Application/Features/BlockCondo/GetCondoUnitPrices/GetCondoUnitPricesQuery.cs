namespace Appraisal.Application.Features.BlockCondo.GetCondoUnitPrices;

public record GetCondoUnitPricesQuery(Guid AppraisalId) : IQuery<GetCondoUnitPricesResult>;
