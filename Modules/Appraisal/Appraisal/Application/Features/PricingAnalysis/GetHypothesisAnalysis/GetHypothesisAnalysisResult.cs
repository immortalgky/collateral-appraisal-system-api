using Appraisal.Domain.Appraisals.Hypothesis;
using Appraisal.Domain.Appraisals.Hypothesis.CostItems;
using Appraisal.Domain.Appraisals.Hypothesis.Summaries;
using Appraisal.Domain.Appraisals.Hypothesis.Uploads;

namespace Appraisal.Application.Features.PricingAnalysis.GetHypothesisAnalysis;

public record GetHypothesisAnalysisResult(
    Guid? HypothesisAnalysisId,
    HypothesisVariant? Variant,
    LandBuildingSummary? LandBuildingSummary,
    CondominiumSummary? CondominiumSummary,
    IReadOnlyList<UploadHistoryDto> Uploads,
    IReadOnlyList<LandBuildingUnitRowDto> LandBuildingRows,
    IReadOnlyList<CondominiumUnitRowDto> CondominiumRows,
    IReadOnlyList<CostItemDto> CostItems,
    string? Remark,
    /// <summary>
    /// System-derived C01: sum of LandAppraisalDetail.TotalLandAreaInSqWa across all land titles
    /// in the property group. Null when the analysis is on a ProjectModel (no land-title chain),
    /// or when the group has no land properties / titles entered yet.
    /// The FE should display this as the authoritative C01 when non-null.
    /// </summary>
    decimal? TotalLandAreaFromTitles = null
);

public record UploadHistoryDto(
    Guid Id,
    string FileName,
    DateTime UploadedAt,
    bool IsActive,
    int RowCount
);

public record LandBuildingUnitRowDto(
    int SequenceNumber,
    string? PlanNo,
    string? HouseNo,
    string? ModelName,
    string? Location,
    int? FloorNo,
    decimal? LandAreaSqWa,
    decimal? UsableAreaSqM,
    decimal? SellingPrice,
    string? Remark1,
    string? Remark2
);

public record CondominiumUnitRowDto(
    int SequenceNumber,
    int? FloorNo,
    string? Building,
    string? AptNo,
    string? Apartment,
    string? ModelType,
    decimal? UsableAreaSqM,
    decimal? SellingPrice,
    string? Remark1,
    string? Remark2
);

public record CostItemDto(
    Guid Id,
    HypothesisCostCategory Category,
    CostItemKind Kind,
    string Description,
    int DisplaySequence,
    decimal Amount,
    decimal? RateAmount,
    decimal? Quantity,
    decimal? RatePercent,
    decimal? CategoryRatio,
    string? ModelName,
    // ── CostOfBuilding categorisation ────────────────────────────────────
    /// <see cref="Appraisal.Domain.Appraisals.Hypothesis.CostItems.HypothesisCostItem.IsBuilding"/>
    bool IsBuilding,
    /// <see cref="Appraisal.Domain.Appraisals.Hypothesis.CostItems.HypothesisCostItem.DepreciationMethod"/>
    string DepreciationMethod,
    // ── CostOfBuilding B-fields (FSD §2.1.3.5.1 Figure 52) ──────────────
    /// <see cref="Appraisal.Domain.Appraisals.Hypothesis.CostItems.HypothesisCostItem.Area"/> B01
    decimal? Area,
    /// <see cref="Appraisal.Domain.Appraisals.Hypothesis.CostItems.HypothesisCostItem.PricePerSqM"/> B02
    decimal? PricePerSqM,
    /// <see cref="Appraisal.Domain.Appraisals.Hypothesis.CostItems.HypothesisCostItem.PriceBeforeDepreciation"/> B03 (computed)
    decimal? PriceBeforeDepreciation,
    /// <see cref="Appraisal.Domain.Appraisals.Hypothesis.CostItems.HypothesisCostItem.Year"/> B04
    int? Year,
    /// <see cref="Appraisal.Domain.Appraisals.Hypothesis.CostItems.HypothesisCostItem.AnnualDepreciationPercent"/> B05
    decimal? AnnualDepreciationPercent,
    /// <see cref="Appraisal.Domain.Appraisals.Hypothesis.CostItems.HypothesisCostItem.TotalDepreciationPercent"/> B06 (computed)
    decimal? TotalDepreciationPercent,
    /// <see cref="Appraisal.Domain.Appraisals.Hypothesis.CostItems.HypothesisCostItem.DepreciationAmount"/> B07 (computed)
    decimal? DepreciationAmount,
    /// <see cref="Appraisal.Domain.Appraisals.Hypothesis.CostItems.HypothesisCostItem.ValueAfterDepreciation"/> B08 (computed)
    decimal? ValueAfterDepreciation,
    // ── Period depreciation child rows ────────────────────────────────────
    IReadOnlyList<DepreciationPeriodDto> DepreciationPeriods
);

/// <summary>
/// A single depreciation period row returned in the get/preview response.
/// </summary>
public record DepreciationPeriodDto(
    Guid Id,
    int Sequence,
    int AtYear,
    int ToYear,
    decimal DepreciationPerYear
);
