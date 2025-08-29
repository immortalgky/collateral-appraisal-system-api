namespace Collateral.Collateral.Shared.Features.UpdateCollateral;

public class UpdateCollateralCommandHandler(ICollateralService collateralService)
    : ICommandHandler<UpdateCollateralCommand, UpdateCollateralResult>
{
    public async Task<UpdateCollateralResult> Handle(
        UpdateCollateralCommand command,
        CancellationToken cancellationToken
    )
    {
        await collateralService.UpdateCollateral(
            command.Id,
            new CollateralMasterDto(
                command.Id,
                command.CollatType,
                command.HostCollatId,
                command.CollateralLand,
                command.LandTitles,
                command.CollateralBuilding,
                command.CollateralCondo,
                command.CollateralMachine,
                command.CollateralVehicle,
                command.CollateralVessel
            ),
            cancellationToken
        );
        return new UpdateCollateralResult(true);
    }
}
