using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.BlockVillage.SaveVillageUnitPrices;

public record SaveVillageUnitPricesCommand(
    Guid AppraisalId,
    List<VillageUnitPriceFlagData> UnitPriceFlags
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;

public record VillageUnitPriceFlagData(
    Guid VillageUnitId,
    bool IsCorner,
    bool IsEdge,
    bool IsNearGarden,
    bool IsOther
);
