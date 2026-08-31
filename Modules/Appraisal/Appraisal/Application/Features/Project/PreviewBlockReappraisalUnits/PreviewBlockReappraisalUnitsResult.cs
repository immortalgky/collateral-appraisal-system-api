namespace Appraisal.Application.Features.Project.PreviewBlockReappraisalUnits;

/// <summary>
/// Dry-run result: summary counts + per-unit classification list.
/// No database writes — safe to call multiple times before committing.
/// </summary>
public record PreviewBlockReappraisalUnitsResult(
    PreviewSummaryDto Summary,
    IReadOnlyList<PreviewUnitDto> Units,
    IReadOnlyList<PreviewAddedUnitDto> AddedUnits
);

/// <summary>
/// Aggregate counts broken down by the four status buckets.
/// All four values sum to <see cref="Total"/>.
/// </summary>
/// <param name="Total">Total units in the working copy.</param>
/// <param name="Sold">Units already marked IsSold (carried from master; untouched by apply).</param>
/// <param name="NewlySold">Not-sold units whose business key is absent from the Excel.</param>
/// <param name="Available">Not-sold units present in the Excel with matching attributes.</param>
/// <param name="MatchDifference">Not-sold units present in the Excel but with one or more attribute differences.</param>
/// <param name="Added">
///   Excel rows whose business key matches no unit in the project. Not part of <see cref="Total"/>:
///   these rows do not exist in the working copy yet. Applying with confirmUpdates=true appends them.
/// </param>
public record PreviewSummaryDto(
    int Total,
    int Sold,
    int NewlySold,
    int Available,
    int MatchDifference,
    int Added
);

/// <summary>
/// Per-unit classification for the reconcile popup table.
/// All unit-identity and attribute fields match the working-copy row so the FE can
/// render them directly without a second fetch.
/// </summary>
public record PreviewUnitDto(
    Guid Id,
    int SequenceNumber,
    // Common
    string? ModelType,
    decimal? UsableArea,
    decimal? SellingPrice,
    // Condo-side (null for L&B)
    int? Floor,
    string? TowerName,
    string? CondoRegistrationNumber,
    string? RoomNumber,
    // L&B-side (null for Condo)
    string? PlotNumber,
    string? HouseNumber,
    int? NumberOfFloors,
    decimal? LandArea,
    // Sale tracking
    bool IsSold,
    // Status bucket: "Sold" | "NewlySold" | "Available" | "MatchDifference"
    string Status,
    // camelCase field names that differ from the incoming Excel row (empty for non-MatchDifference)
    IReadOnlyList<string> DiffFields,
    // The Excel's value for each field named in DiffFields, so the caller can show old vs new
    // rather than only that something changed. Empty for non-MatchDifference rows.
    IReadOnlyDictionary<string, object?> IncomingValues
);

/// <summary>
/// An Excel row that matches no unit in the project. Rendered alongside the existing units so the
/// user can see what applying the file would add before confirming it.
/// </summary>
public record PreviewAddedUnitDto(
    // Common
    string? ModelType,
    decimal? UsableArea,
    decimal? SellingPrice,
    // Condo-side (null for L&B)
    int? Floor,
    string? TowerName,
    string? CondoRegistrationNumber,
    string? RoomNumber,
    // L&B-side (null for Condo)
    string? PlotNumber,
    string? HouseNumber,
    int? NumberOfFloors,
    decimal? LandArea
);
