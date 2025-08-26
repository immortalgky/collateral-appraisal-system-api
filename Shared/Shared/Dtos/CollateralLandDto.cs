namespace Shared.Dtos;

public record CollateralLandDto(
    long CollatId,
    CoordinateDto Coordinate,
    CollateralLocationDto CollateralLocation,
    string LandDesc
);
