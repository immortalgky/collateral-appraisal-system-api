namespace Appraisal.Application.Features.DecisionSummary.SaveDecisionSummary;

public record SaveDecisionSummaryResponse(
    Guid Id,
    Guid AppraisalId,
    bool? IsPriceVerified,
    string? ConditionType,
    string? Condition,
    string? RemarkType,
    string? Remark,
    string? AppraiserOpinionType,
    string? AppraiserOpinion,
    string? CommitteeOpinionType,
    string? CommitteeOpinion,
    decimal? TotalAppraisalPriceReview,
    string? AdditionalAssumptions
);
