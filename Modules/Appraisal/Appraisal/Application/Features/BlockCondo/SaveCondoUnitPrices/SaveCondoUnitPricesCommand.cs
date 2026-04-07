using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.BlockCondo.SaveCondoUnitPrices;

public record SaveCondoUnitPricesCommand(
    Guid AppraisalId,
    List<CondoUnitPriceFlagData> UnitPriceFlags
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;

public record CondoUnitPriceFlagData(
    Guid CondoUnitId,
    bool IsCorner,
    bool IsEdge,
    bool IsPoolView,
    bool IsSouth,
    bool IsOther
);
