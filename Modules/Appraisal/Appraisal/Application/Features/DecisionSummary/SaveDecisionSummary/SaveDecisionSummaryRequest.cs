namespace Appraisal.Application.Features.DecisionSummary.SaveDecisionSummary;

public record SaveDecisionSummaryRequest(
    bool? IsPriceVerified = null,
    string? ConditionType = null,
    string? Condition = null,
    string? RemarkType = null,
    string? Remark = null,
    string? ExternalAppraiserOpinionType = null,
    string? ExternalAppraiserOpinion = null,
    string? CommitteeOpinionType = null,
    string? CommitteeOpinion = null,
    string? InternalAppraiserOpinionType = null,
    string? InternalAppraiserOpinion = null,
    decimal? TotalAppraisalPriceReview = null,
    string? AdditionalAssumptions = null
);
