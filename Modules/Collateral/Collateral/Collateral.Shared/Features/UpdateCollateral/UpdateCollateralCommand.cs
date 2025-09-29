namespace Collateral.Collateral.Shared.Features.UpdateCollateral;

public record UpdateCollateralCommand(
    long Id,
    string CollatType,
    long? HostCollatId,
    CollateralMachineDto? CollateralMachine,
    CollateralVehicleDto? CollateralVehicle,
    CollateralVesselDto? CollateralVessel,
    CollateralLandDto? CollateralLand,
    CollateralBuildingDto? CollateralBuilding,
    CollateralCondoDto? CollateralCondo,
    List<LandTitleDto> LandTitles
) : ICommand<UpdateCollateralResult>;
