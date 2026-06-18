namespace Appraisal.Application.Features.Appraisals.GetAssetSummary;

public record GetAssetSummaryResult(
    List<AssetSummaryItemDto>? Items,
    List<AssetSummaryGroupDto>? Groups
);

public record AssetSummaryItemDto(
    Guid Id,
    string PropertyType,
    string? AssetDetail,
    decimal? Area,
    decimal? PricePerUnit,
    decimal? EstimatedPrice,
    decimal? CurrentPrice,
    int GroupSet,
    bool? IsPricesCurrent
);

public record AssetSummaryGroupDto(
    Guid Id,
    int GroupSet,
    string? AssetGroupDetail,
    decimal? SumEstimatedPrice,
    decimal? RoundEstimatedPrice,
    decimal? SumCurrentPrice,
    decimal? RoundCurrentPrice
);
