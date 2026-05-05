namespace Collateral.Application.Features.CollateralMasters.RestoreMaster;

public record RestoreCollateralMasterCommand(Guid Id, string Reason) : ICommand<RestoreCollateralMasterResult>;

public record RestoreCollateralMasterResult(Guid Id);
