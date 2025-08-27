namespace Appraisal.Contracts.Appraisals.Dto;

public record MachineAppraisalAdditionalInfoDto(
    long ApprId,

    // PurposeAndLocationMachine
    string? Assignment,
    string? ApprCollatPurpose,
    string? ApprDate,
    string? ApprCollatType,

    // MachineDetail.GeneralMachinery
    string? Industrial,
    int? SurveyNo,
    int? ApprNo,

    // MachineDetail.AtSurveyDate
    int? Installed,
    string? ApprScrap,
    int? NoOfAppraise,
    int? NotInstalled,
    string? Maintenance,
    string? Exterior,
    string? Performance,
    bool? MarketDemand,
    string? MarketDemandRemark,

    // MachineDetail.RightsAndConditionsOfLegalRestrictions
    string? Proprietor,
    string? Owner,
    string? MachineLocation,
    string? Obligation,
    string? Other
);