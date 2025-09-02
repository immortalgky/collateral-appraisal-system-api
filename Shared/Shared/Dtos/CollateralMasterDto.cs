namespace Shared.Dtos;

public record CollateralMasterDto(
    long CollatId,
    string CollatType,
    long? HostCollatId,
    CollateralLandDto? CollateralLand,
    List<LandTitleDto>? LandTitles,
    CollateralBuildingDto? CollateralBuilding,
    CollateralCondoDto? CollateralCondo,
    CollateralMachineDto? CollateralMachine,
    CollateralVehicleDto? CollateralVehicle,
    CollateralVesselDto? CollateralVessel,
    List<long> ReqIds
);