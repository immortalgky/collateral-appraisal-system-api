namespace Appraisal.Application.Features.Appraisals.GetAssetSummary;

public record GetAssetSummaryResponse(
    List<AssetSummaryItemResponse> Items,
    List<AssetSummaryGroupResponse> Groups
);

public record AssetSummaryItemResponse(
    Guid Id,
    string? PropertyType,
    string? AssetDetail,
    decimal? Area,
    decimal? PricePerUnit,
    decimal? EstimatedPrice,
    decimal? CurrentPrice,
    int? GroupSet,
    bool IsPricesCurrent
);

public record AssetSummaryGroupResponse(
    Guid Id,
    int GroupSet,
    string? AssetGroupDetail,
    decimal SumEstimatedPrice,
    decimal RoundEstimatedPrice,
    decimal SumCurrentPrice,
    decimal RoundCurrentPrice
);
