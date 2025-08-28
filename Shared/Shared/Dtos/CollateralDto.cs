namespace Shared.Dtos;

public record CollateralDto
{
    public CollateralLandDto? CollateralLand { get; init; }
    public List<LandTitleDto>? LandTitles { get; init; }
    public CollateralBuildingDto? CollateralBuilding { get; init; }
    public CollateralCondoDto? CollateralCondo { get; init; }
    public CollateralMachineDto? CollateralMachine { get; init; }
    public CollateralVehicleDto? CollateralVehicle { get; init; }
    public CollateralVesselDto? CollateralVessel { get; init; }
}