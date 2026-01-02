namespace Collateral.Collateral.Shared.Features.DeleteCollateral;

public record DeleteCollateralCommand(long Id) : ICommand<DeleteCollateralResult>;
