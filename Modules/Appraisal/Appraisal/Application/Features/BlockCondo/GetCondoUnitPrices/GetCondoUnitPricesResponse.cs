namespace Appraisal.Application.Features.BlockCondo.GetCondoUnitPrices;

public record GetCondoUnitPricesResponse(IReadOnlyList<CondoUnitPriceDto> UnitPrices);
