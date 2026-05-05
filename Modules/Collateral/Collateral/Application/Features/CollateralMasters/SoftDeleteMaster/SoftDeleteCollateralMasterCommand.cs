namespace Collateral.Application.Features.CollateralMasters.SoftDeleteMaster;

public record SoftDeleteCollateralMasterCommand(Guid Id, string Reason) : ICommand<SoftDeleteCollateralMasterResult>;

public record SoftDeleteCollateralMasterResult(Guid Id);
