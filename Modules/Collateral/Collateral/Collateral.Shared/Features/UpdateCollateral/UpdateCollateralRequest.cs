namespace Collateral.Collateral.Shared.Features.UpdateCollateral;

public record UpdateCollateralRequest(
    string CollatType,
    long? HostCollatId,
    CollateralMachineDto? CollateralMachine,
    CollateralVehicleDto? CollateralVehicle,
    CollateralVesselDto? CollateralVessel,
    CollateralLandDto? CollateralLand,
    CollateralBuildingDto? CollateralBuilding,
    CollateralCondoDto? CollateralCondo,
    List<LandTitleDto>? LandTitles
);
