namespace Collateral.Collateral.Shared.Features.CreateCollateral;

public record CreateCollateralCommand(
    string CollatType,
    CollateralLandDto? CollateralLand,
    List<LandTitleDto>? LandTitles,
    CollateralBuildingDto? CollateralBuilding,
    CollateralCondoDto? CollateralCondo,
    CollateralMachineDto? CollateralMachine,
    CollateralVehicleDto? CollateralVehicle,
    CollateralVesselDto? CollateralVessel
) : ICommand<CreateCollateralResult>;
