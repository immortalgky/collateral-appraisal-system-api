namespace Shared.Dtos;

public record CollateralVehicleDto(
    CollateralPropertyDto CollateralVehicleProperty,
    CollateralDetailDto CollateralVehicleDetail,
    CollateralSizeDto CollateralVehicleSize,
    string ChassisNo
);