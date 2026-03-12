namespace Appraisal.Application.Features.DecisionSummary.GetDecisionSummary;

public record GetDecisionSummaryResponse(
    IReadOnlyList<ApproachMatrixGroup> ApproachMatrix,
    decimal TotalAppraisalPrice,
    decimal ForceSellingPrice,
    decimal BuildingInsurance,
    IReadOnlyList<GovernmentPriceRow> GovernmentPrices,
    decimal GovernmentPriceTotalArea,
    decimal GovernmentPriceAvgPerSqWa,
    decimal? TotalAppraisalPriceReview,
    decimal? ForceSellingPriceReview,
    decimal BuildingInsuranceReview,
    string? CommitteeName,
    string? ReviewStatus,
    Guid? ReviewId,
    IReadOnlyList<DecisionApprovalListItem>? ApprovalList,
    Guid? DecisionId,
    bool? IsPriceVerified,
    string? ConditionType,
    string? Condition,
    string? RemarkType,
    string? Remark,
    string? AppraiserOpinionType,
    string? AppraiserOpinion,
    string? CommitteeOpinionType,
    string? CommitteeOpinion,
    string? AdditionalAssumptions
);
