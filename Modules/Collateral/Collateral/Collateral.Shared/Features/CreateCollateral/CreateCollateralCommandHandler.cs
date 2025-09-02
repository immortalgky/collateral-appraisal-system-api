namespace Collateral.Collateral.Shared.Features.CreateCollateral;

public class CreateCollateralCommandHandler(ICollateralService collateralService)
    : ICommandHandler<CreateCollateralCommand, CreateCollateralResult>
{
    public async Task<CreateCollateralResult> Handle(
        CreateCollateralCommand command,
        CancellationToken cancellationToken
    )
    {
        var collateralMaster = await collateralService.CreateCollateral(
            new CollateralMasterDto(
                0,
                command.CollatType,
                0,
                command.CollateralLand,
                command.LandTitles,
                command.CollateralBuilding,
                command.CollateralCondo,
                command.CollateralMachine,
                command.CollateralVehicle,
                command.CollateralVessel,
                [command.ReqId]
            ),
            cancellationToken
        );
        return new CreateCollateralResult(collateralMaster.Id);
    }
}
