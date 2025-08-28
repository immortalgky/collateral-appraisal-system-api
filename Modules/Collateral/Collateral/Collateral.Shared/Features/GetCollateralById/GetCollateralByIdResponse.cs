namespace Collateral.Collateral.Shared.Features.GetCollateralById;

public record GetCollateralByIdResponse(
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
);
