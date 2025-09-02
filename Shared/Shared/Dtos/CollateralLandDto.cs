namespace Shared.Dtos;

public record CollateralLandDto(
    CoordinateDto Coordinate,
    CollateralLocationDto CollateralLocation,
    string LandDesc
);
