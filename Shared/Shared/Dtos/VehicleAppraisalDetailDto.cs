namespace Shared.Dtos;

public record VehicleAppraisalDetailDto(
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