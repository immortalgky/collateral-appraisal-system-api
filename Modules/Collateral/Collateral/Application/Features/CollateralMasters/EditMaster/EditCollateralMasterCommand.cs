using Collateral.CollateralMasters.Models;

namespace Collateral.Application.Features.CollateralMasters.EditMaster;

public record EditCollateralMasterCommand(
    Guid Id,
    string? OwnerName,
    string Reason,
    LandAdminEdit? LandEdit,
    CondoAdminEdit? CondoEdit,
    LeaseholdAdminEdit? LeaseholdEdit,
    MachineAdminEdit? MachineEdit
) : ICommand<EditCollateralMasterResult>;

public record EditCollateralMasterResult(Guid Id);
