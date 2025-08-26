namespace Shared.Dtos;

public record CollateralVesselDto(
    long CollatId,
    CollateralPropertyDto CollateralVesselProperty,
    CollateralDetailDto CollateralVesselDetail,
    CollateralSizeDto CollateralVesselSize,
    long ChassisNo
);
