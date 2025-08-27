namespace Appraisal.Contracts.Appraisals.Dto;

public record AppraisalDto(
    List<LandAppraisalDetailDto> LandAppraisalDetail,
    BuildingAppraisalDetailDto BuildingAppraisalDetail,
    BuildingAppraisalSurfaceDto BuildingAppraisalSurface,
    BuildingAppraisalDepreciationDetailDto BuildingAppraisalDepreciationDetail,
    List<BuildingAppraisalDepreciationPeriodDto> BuildingAppraisalDepreciationPeriod,
    CondoAppraisalDetailDto CondoAppraisalDetail,
    CondoAppraisalAreaDetailDto CondoAppraisalAreaDetail,
    MachineAppraisalDetailDto MachineAppraisalDetail,
    MachineAppraisalAdditionalInfoDto MachineAppraisalAdditionalInfo,
    VehicleAppraisalDetailDto VehicleAppraisalDetail,
    VesselAppraisalDetailDto VesselAppraisalDetail
);