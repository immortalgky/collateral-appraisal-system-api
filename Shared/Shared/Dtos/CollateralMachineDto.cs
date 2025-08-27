namespace Shared.Dtos;

public record CollateralMachineDto(
    long CollatId,
    CollateralPropertyDto CollateralMachineProperty,
    CollateralDetailDto CollateralMachineDetail,
    CollateralSizeDto CollateralMachineSize,
    string ChassisNo
);