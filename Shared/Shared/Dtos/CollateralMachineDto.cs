namespace Shared.Dtos;

public record CollateralMachineDto(
    CollateralPropertyDto CollateralMachineProperty,
    CollateralDetailDto CollateralMachineDetail,
    CollateralSizeDto CollateralMachineSize,
    string ChassisNo
);