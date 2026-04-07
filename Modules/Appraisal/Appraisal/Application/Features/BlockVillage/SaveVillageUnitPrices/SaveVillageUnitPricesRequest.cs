namespace Appraisal.Application.Features.BlockVillage.SaveVillageUnitPrices;

public record SaveVillageUnitPricesRequest(
    List<VillageUnitPriceFlagData> UnitPriceFlags
);
