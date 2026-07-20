namespace Appraisal.Application.Features.DecisionSummary.GetDecisionSummary;

public record GetDecisionSummaryResult(
    // Approach Matrix (read-only) — empty list for block appraisals
    IReadOnlyList<ApproachMatrixGroup> ApproachMatrix,

    // Summary Totals (read-only, calculated)
    decimal TotalAppraisalPrice,
    decimal ForceSellingPrice,
    decimal BuildingInsurance,

    // Government Appraisal Prices (read-only)
    IReadOnlyList<GovernmentPriceRow> GovernmentPrices,
    decimal GovernmentPriceTotalArea,     // all titles incl. missing-from-survey (total land area)
    decimal GovernmentPriceSurveyedArea,  // non-missing titles only — the area the AVG is computed over
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
    string? AdditionalAssumptions,

    // Block appraisal fields (null for normal appraisals)
    bool IsBlock,
    IReadOnlyList<BlockApproachMatrixRow>? BlockApproachMatrix,
    IReadOnlyList<BlockModelPriceRow>? BlockModelPrices,

    // Construction summary (null when no under-construction buildings or block appraisal)
    ConstructionSummaryData? ConstructionSummary,

    // Appraisal date — most recent non-cancelled appointment date
    DateTime? AppraisalDate
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

public record BlockApproachMatrixRow(
    Guid ProjectModelId,
    string? ModelName,
    decimal? MarketValue,
    decimal? CostValue,
    decimal? IncomeValue,
    decimal? ResidualValue,
    string? SelectedApproach,
    decimal ModelTotalAppraisalPrice
);

public record BlockModelPriceRow(
    Guid ProjectModelId,
    string? ModelName,
    int UnitCount,
    decimal TotalAppraisalPrice,
    decimal ForceSellingPrice,
    decimal BuildingInsurance
);

public record ConstructionSummaryData(
    string? Village,
    IReadOnlyList<ConstructionSummaryRow> Rows,
    IReadOnlyList<ConstructionBuildingRow> Buildings,
    IReadOnlyList<ConstructionCompletedBuildingRow> CompletedBuildings
);

public record ConstructionSummaryRow(
    string Label,
    decimal ConstructionProgressPct,
    decimal TotalAppraisalValue,
    decimal TotalLandValue,
    decimal TotalBuildingValue,
    decimal BuildingValueConstructing
);

// หนึ่งแถวต่อหนึ่ง AppraisalProperty ที่มีการตรวจงวดงาน (ConstructionInspection 1:1 กับ AppraisalProperty)
public record ConstructionBuildingRow(
    Guid AppraisalPropertyId,
    string? HouseNumber,
    string? TitleNumber,
    string? ModelName,
    decimal TotalValue,
    decimal PreviousValue,
    decimal CurrentValue,
    decimal PreviousProgressPct,
    decimal CurrentProgressPct
);

// อาคารที่สร้างเสร็จ 100% ก่อนการตรวจงวดงาน = อาคารที่ไม่มีแถว ConstructionInspection
public record ConstructionCompletedBuildingRow(
    Guid AppraisalPropertyId,
    string? HouseNumber,
    string? TitleNumber,
    string? ModelName,
    decimal AppraisalValue
);
