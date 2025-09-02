namespace Collateral.Collateral.Shared.Features.CreateCollateral;

public record CreateCollateralRequest(
    string CollatType,
    CollateralLandDto? CollateralLand,
    List<LandTitleDto>? LandTitles,
    CollateralBuildingDto? CollateralBuilding,
    CollateralCondoDto? CollateralCondo,
    CollateralMachineDto? CollateralMachine,
    CollateralVehicleDto? CollateralVehicle,
    CollateralVesselDto? CollateralVessel,
    long ReqId
);
