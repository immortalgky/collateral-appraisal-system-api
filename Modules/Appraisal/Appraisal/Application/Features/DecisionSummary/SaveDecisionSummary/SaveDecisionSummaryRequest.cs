namespace Appraisal.Application.Features.DecisionSummary.SaveDecisionSummary;

public record SaveDecisionSummaryRequest(
    bool? IsPriceVerified = null,
    string? ConditionType = null,
    string? Condition = null,
    string? RemarkType = null,
    string? Remark = null,
    string? AppraiserOpinionType = null,
    string? AppraiserOpinion = null,
    string? CommitteeOpinionType = null,
    string? CommitteeOpinion = null,
    decimal? TotalAppraisalPriceReview = null,
    string? AdditionalAssumptions = null
);
