namespace Appraisal.Domain.Projects;

/// <summary>
/// Single source of truth for the fire-insurance coverage-amount lookup by building condition.
/// Used by both the pricing-assumptions query (display) and the unit-price calculator.
/// </summary>
public static class CoverageByCondition
{
    public static readonly IReadOnlyDictionary<string, decimal> Map =
        new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            // Condo conditions
            ["LessThan8Floors"] = 25_000m,
            ["GreaterThan8Floors"] = 30_000m,
            ["LessThan8FloorsWithMezzanine"] = 35_000m,
            ["GreaterThan8FloorsWithMezzanine"] = 40_000m,
            // LandAndBuilding building-type conditions
            ["OneTwoStoreyTownhouse"] = 10_000m,
            ["ThreeStoreyTownhouse"] = 12_000m,
            ["SemiDetachedHouse"] = 12_000m,
            ["SingleHouseAreaLessThan150"] = 15_000m,
            ["SingleHouseArea150To200"] = 17_000m,
            ["SingleHouseArea200To400"] = 19_000m,
            ["SingleHouseArea400To500"] = 25_000m,
            ["SingleHouseAreaGreaterThan500"] = 30_000m,
        };

    /// <summary>Returns the coverage amount for a condition, or null if the condition is unknown.</summary>
    public static decimal? Lookup(string? condition)
    {
        if (string.IsNullOrEmpty(condition)) return null;
        return Map.TryGetValue(condition, out var amount) ? amount : null;
    }
}
