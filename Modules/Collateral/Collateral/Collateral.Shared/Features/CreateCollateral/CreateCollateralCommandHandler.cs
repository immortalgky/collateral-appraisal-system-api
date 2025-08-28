namespace Collateral.Collateral.Shared.Features.CreateCollateral;

public class CreateCollateralCommandHandler(ICollateralService collateralService)
    : ICommandHandler<CreateCollateralCommand, CreateCollateralResult>
{
    public async Task<CreateCollateralResult> Handle(
        CreateCollateralCommand command,
        CancellationToken cancellationToken
    )
    {
        _ = Enum.TryParse(command.CollatType, out CollateralType collatType);
        var collateralMaster = await collateralService.CreateCollateral(
            collatType,
            new CollateralDto()
            {
                CollateralLand = command.CollateralLand,
                LandTitles = command.LandTitles,
                CollateralBuilding = command.CollateralBuilding,
                CollateralCondo = command.CollateralCondo,
                CollateralMachine = command.CollateralMachine,
                CollateralVehicle = command.CollateralVehicle,
                CollateralVessel = command.CollateralVessel,
            },
            cancellationToken
        );
        return new CreateCollateralResult(collateralMaster.Id);
    }
}
