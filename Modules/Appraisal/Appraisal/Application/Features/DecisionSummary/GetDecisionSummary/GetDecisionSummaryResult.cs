namespace Appraisal.Application.Features.DecisionSummary.GetDecisionSummary;

public record GetDecisionSummaryResult(
    // Approach Matrix (read-only) — empty list for block appraisals
    IReadOnlyList<ApproachMatrixGroup> ApproachMatrix,

    // Summary Totals (read-only, calculated)
    decimal TotalAppraisalPrice,
    decimal ForceSellingPrice,
    // RESOLVED rate (e.g. 70.00 = 70%) — never null: override -> project assumption (block only)
    // -> system default, already applied to ForceSellingPrice above. DISPLAY-only — do not post
    // this back as the save's override, that would permanently freeze it (see ForceSellingRateOverride).
    decimal ForceSellingRate,
    // RAW per-appraisal override (ValuationAnalyses.ForceSaleRate) — null means "no override, using
    // the resolved default/project rate". This is what a save should round-trip as the command's
    // ForceSellingRate so an unrelated edit (e.g. Remark) doesn't silently stamp an override.
    decimal? ForceSellingRateOverride,
    decimal BuildingInsurance,

    // Government Appraisal Prices (read-only)
    IReadOnlyList<GovernmentPriceRow> GovernmentPrices,
    decimal GovernmentPriceTotalArea,     // all titles incl. missing-from-survey (total land area)
    decimal GovernmentPriceSurveyedArea,  // non-missing titles only — the area the AVG is computed over
    decimal GovernmentPriceAvgPerSqWa,

    // Condo Government Appraisal Prices (read-only) — kept separate from the land list above:
    // land area is in Sq.Wa and condo area is in sq.m., so mixing them would corrupt the land AVG.
    // Only ONE area total (no IsMissingFromSurvey equivalent on condo — every unit has a usable area).
    IReadOnlyList<CondoGovernmentPriceRow> CondoGovernmentPrices,
    decimal CondoGovernmentPriceTotalArea,   // sq.m.
    decimal CondoGovernmentPriceAvgPerSqm,   // weighted: totalPrice / totalArea

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
    string? ExternalAppraiserOpinionType,
    string? ExternalAppraiserOpinion,
    string? CommitteeOpinionType,
    string? CommitteeOpinion,
    string? InternalAppraiserOpinionType,
    string? InternalAppraiserOpinion,
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

public record CondoGovernmentPriceRow(
    string? TitleNumber,
    string? RoomNumber,
    decimal? UsableArea,
    decimal? GovernmentPricePerSqm,
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
