namespace Appraisal.Application.Features.BlockCondo.SaveCondoUnitPrices;

public record SaveCondoUnitPricesRequest(
    List<CondoUnitPriceFlagData> UnitPriceFlags
);
