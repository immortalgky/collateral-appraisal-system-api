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
    string? Remark
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
    decimal? LandAreaSqWa,
    decimal? SellingPrice
);

public record CondominiumUnitRowDto(
    int SequenceNumber,
    int? FloorNo,
    string? Building,
    string? AptNo,
    string? ModelType,
    decimal? UsableAreaSqM,
    decimal? SellingPrice
);

public record CostItemDto(
    Guid Id,
    HypothesisCostCategory Category,
    string Description,
    int DisplaySequence,
    decimal Amount,
    decimal? RateAmount,
    decimal? Quantity,
    decimal? RatePercent,
    decimal? CategoryRatio,
    string? ModelName
);
