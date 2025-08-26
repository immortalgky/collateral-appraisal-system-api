namespace Shared.Dtos;

public record AppraisalDto(
    CollateralMasterDto CollateralMaster,
    CollateralLandDto CollateralLand,
    LandAppraisalDetailDto LandAppraisalDetailDto,
    LandTitleDto LandTitle,
    CollateralBuildingDto CollateralBuilding,
    BuildingAppraisalDetailDto BuildingAppraisalDetail,
    BuildingAppraisalSurfaceDto BuildingAppraisalSurface,
    BuildingAppraisalDepreciationDetailDto BuildingAppraisalDepreciationDetail,
    BuildingAppraisalDepreciationPeriodDto BuildingAppraisalDepreciationPeriod,
    CollateralCondoDto CollateralCondo,
    CondoAppraisalDetailDto CondoAppraisalDetail,
    CondoAppraisalAreaDetailDto CondoAppraisalAreaDetail,
    CollateralMachineDto CollateralMachine,
    MachineAppraisalDetailDto MachineAppraisalDetail,
    MachineAppraisalAdditionalInfoDto MachineAppraisalAdditionalInfo,
    CollateralVehicleDto CollateralVehicle,
    VehicleAppraisalDetailDto VehicleAppraisalDetail,
    CollateralVesselDto CollateralVessel,
    VesselAppraisalDetailDto VesselAppraisalDetail
);