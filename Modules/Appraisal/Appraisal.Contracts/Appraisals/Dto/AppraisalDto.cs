namespace Appraisal.Contracts.Appraisals.Dto;

public record RequestAppraisalDto(
    long ApprId,
    long? RequestId,
    long? CollateralId,
    string Type,
    LandAppraisalDetailDto? LandAppraisalDetail,
    BuildingAppraisalDetailDto? BuildingAppraisalDetail,
    CondoAppraisalDetailDto? CondoAppraisalDetail,
    MachineAppraisalDetailDto? MachineAppraisalDetail,
    MachineAppraisalAdditionalInfoDto? MachineAppraisalAdditionalInfo,
    VehicleAppraisalDetailDto? VehicleAppraisalDetail,
    VesselAppraisalDetailDto? VesselAppraisalDetail
);