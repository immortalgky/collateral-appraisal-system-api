namespace Appraisal.Contracts.Appraisals.Dto;

public record VesselAppraisalDetailDto(
    long ApprId,
    bool CanUse,
    string Location,
    string ConditionUse,
    string UsePurpose,
    string Part,
    string Remark,
    string Other,
    string AppraiserOpinion
);