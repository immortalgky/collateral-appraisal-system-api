namespace Shared.Dtos;

public record CollateralVehicleDto(
    long CollatId,
    CollateralPropertyDto CollateralVehicleProperty,
    CollateralDetailDto CollateralVehicleDetail,
    CollateralSizeDto CollateralVehicleSize,
    string ChassisNo
);