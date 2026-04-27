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
            ["LessThan8Floors"] = 25_000m,
            ["GreaterThan8Floors"] = 30_000m,
            ["LessThan8FloorsWithMezzanine"] = 35_000m,
            ["GreaterThan8FloorsWithMezzanine"] = 40_000m,
        };

    /// <summary>Returns the coverage amount for a condition, or null if the condition is unknown.</summary>
    public static decimal? Lookup(string? condition)
    {
        if (string.IsNullOrEmpty(condition)) return null;
        return Map.TryGetValue(condition, out var amount) ? amount : null;
    }
}
