namespace Shared.Dtos;

public record CollateralMasterDto(
    string CollatType,
    long? HostCollatId,
    CollateralLandDto? CollateralLand,
    List<LandTitleDto> LandTitle,
    CollateralBuildingDto? CollateralBuilding,
    CollateralCondoDto? CollateralCondo,
    CollateralMachineDto? CollateralMachine,
    CollateralVehicleDto? CollateralVehicle,
    CollateralVesselDto? CollateralVessel
);