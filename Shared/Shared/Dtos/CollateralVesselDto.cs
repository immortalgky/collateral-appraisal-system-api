namespace Shared.Dtos;

public record CollateralVesselDto(
    CollateralPropertyDto CollateralVesselProperty,
    CollateralDetailDto CollateralVesselDetail,
    CollateralSizeDto CollateralVesselSize
);
