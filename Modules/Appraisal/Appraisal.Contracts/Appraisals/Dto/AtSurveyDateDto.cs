namespace Appraisal.Contracts.Appraisals.Dto;

public record AtSurveyDateDto(
    int? Installed,
    string? ApprScrap,
    int? NoOfAppraise,
    int? NotInstalled,
    string? Maintenance,
    string? Exterior,
    string? Performance,
    bool? MarketDemand,
    string? MarketDemandRemark
);
