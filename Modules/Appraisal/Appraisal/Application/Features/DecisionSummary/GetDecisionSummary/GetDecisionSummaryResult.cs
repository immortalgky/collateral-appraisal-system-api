namespace Appraisal.Application.Features.DecisionSummary.GetDecisionSummary;

public record GetDecisionSummaryResult(
    // Approach Matrix (read-only)
    IReadOnlyList<ApproachMatrixGroup> ApproachMatrix,

    // Summary Totals (read-only, calculated)
    decimal TotalAppraisalPrice,
    decimal ForceSellingPrice,
    decimal BuildingInsurance,

    // Government Appraisal Prices (read-only)
    IReadOnlyList<GovernmentPriceRow> GovernmentPrices,
    decimal GovernmentPriceTotalArea,
    decimal GovernmentPriceAvgPerSqWa,

    // Review fields (sourced from ValuationAnalyses — populated by event handler, overridden by Book Verification save)
    decimal? TotalAppraisalPriceReview,
    decimal? ForceSellingPriceReview,
    decimal? BuildingInsuranceReview,

    // Committee Approval
    string? CommitteeName,
    string? ReviewStatus,
    Guid? ReviewId,
    IReadOnlyList<DecisionApprovalListItem>? ApprovalList,

    // Stored Decision fields
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

public record ApproachMatrixGroup(
    Guid PropertyGroupId,
    int GroupNumber,
    decimal? GroupSummaryValue,
    IReadOnlyList<ApproachItem> Approaches
);

public record ApproachItem(
    string ApproachType,
    decimal? ApproachValue,
    bool IsSelected
);

public record GovernmentPriceRow(
    string? TitleNumber,
    decimal? AreaSquareWa,
    bool IsMissingFromSurvey,
    decimal? GovernmentPricePerSqWa,
    decimal? GovernmentPrice
);

public record DecisionApprovalListItem(
    Guid CommitteeMemberId,
    string MemberName,
    string Role,
    string? Vote,
    string VoteLabel,
    string? Remark,
    DateTime? VotedAt
);
