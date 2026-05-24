namespace Appraisal.Application.Features.BlockUnitMaintenance.GetBlockUnitMaintenanceUnits;

public record GetBlockUnitMaintenanceUnitsQuery(Guid ProjectId)
    : IQuery<BlockUnitMaintenanceDetailDto?>;
