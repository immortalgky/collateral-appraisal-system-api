namespace Appraisal.Application.Features.DecisionSummary.GetDecisionSummary;

public record GetDecisionSummaryResult(
    // Approach Matrix (read-only)
    IReadOnlyList<ApproachMatrixRow> ApproachMatrix,

    // Summary Totals (read-only, calculated)
    decimal TotalAppraisalPrice,
    decimal ForceSellingPrice,
    decimal BuildingInsurance,

    // Government Appraisal Prices (read-only)
    IReadOnlyList<GovernmentPriceRow> GovernmentPrices,
    decimal GovernmentPriceTotalArea,
    decimal GovernmentPriceAvgPerSqWa,

    // Review fields (read-only, calculated from stored TotalAppraisalPriceReview)
    decimal? TotalAppraisalPriceReview,
    decimal? ForceSellingPriceReview,
    decimal BuildingInsuranceReview,

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

public record ApproachMatrixRow(
    Guid PropertyGroupId,
    int GroupNumber,
    string ApproachType,
    decimal? FinalValue,
    decimal? FinalValueRounded,
    decimal? GroupSummaryValue
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
