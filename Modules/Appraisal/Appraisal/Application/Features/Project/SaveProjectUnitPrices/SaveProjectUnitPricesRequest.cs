namespace Appraisal.Application.Features.Project.SaveProjectUnitPrices;

/// <summary>
/// Request body for saving location flags on project unit prices.
/// Superset of Condo (IsPoolView, IsSouth) and LandAndBuilding (IsNearGarden) flags.
/// Type-specific flags are ignored by the domain method for the opposing project type.
/// </summary>
public record SaveProjectUnitPricesRequest(
    List<ProjectUnitPriceFlagData> UnitPriceFlags
);

/// <summary>
/// Location flags for a single project unit.
/// IsPoolView and IsSouth are Condo-only; IsNearGarden is LandAndBuilding-only.
/// Supply only the flags relevant to the project type; unused flags default to false.
/// </summary>
public record ProjectUnitPriceFlagData(
    Guid ProjectUnitId,
    bool IsCorner,
    bool IsEdge,
    bool IsOther,
    // Condo-only
    bool IsPoolView = false,
    bool IsSouth = false,
    // LB-only
    bool IsNearGarden = false
);
