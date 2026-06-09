namespace Collateral.Application.Features.BlockUnitMaintenance.GetBlockUnitMaintenanceUnits;

public record GetBlockUnitMaintenanceUnitsQuery(Guid CollateralMasterId)
    : IQuery<BlockUnitMaintenanceDetailDto?>;
