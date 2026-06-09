namespace Appraisal.Application.Features.Project.UploadBlockReappraisalUnits;

/// <summary>
/// Shared matching logic for the block-reappraisal Excel reconcile flow.
/// Used by both the dry-run preview and the apply (upload) handler.
/// </summary>
public static class BlockReappraisalMatcher
{
    private const decimal DecimalTolerance = 0.01m;

    /// <summary>
    /// Builds a normalized business key for matching existing and incoming units.
    ///
    /// Condo:  CondoRegistrationNumber (trimmed, lowercased) when non-empty;
    ///         otherwise (TowerName|RoomNumber) composite.
    ///
    /// L&amp;B:  PlotNumber (trimmed, lowercased) when non-empty;
    ///         otherwise HouseNumber.
    /// </summary>
    public static string BuildKey(ProjectUnit unit, ProjectType projectType)
    {
        if (projectType == ProjectType.Condo)
        {
            var reg = unit.CondoRegistrationNumber?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(reg))
                return reg;

            var tower = unit.TowerName?.Trim().ToLowerInvariant() ?? string.Empty;
            var room = unit.RoomNumber?.Trim().ToLowerInvariant() ?? string.Empty;
            return $"{tower}|{room}";
        }
        else
        {
            var plot = unit.PlotNumber?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(plot))
                return plot;

            return unit.HouseNumber?.Trim().ToLowerInvariant() ?? string.Empty;
        }
    }

    /// <summary>
    /// A key is "blank" (no usable identity) when it is empty/whitespace or the empty
    /// Condo composite "|". Such units cannot be safely matched.
    /// </summary>
    public static bool IsBlankKey(string key) =>
        string.IsNullOrWhiteSpace(key) || key == "|";

    /// <summary>
    /// Compares the non-identity attributes of an existing unit against an incoming Excel row.
    /// Returns true when at least one attribute differs.
    /// <paramref name="diffFields"/> contains the camelCase field names that differ,
    /// for use by the frontend to highlight specific cells.
    ///
    /// Compared fields:
    ///   - modelType (case-insensitive trim)
    ///   - sellingPrice (decimal within <see cref="DecimalTolerance"/>)
    ///   - usableArea  (decimal within <see cref="DecimalTolerance"/>)
    ///   - Condo only: floor (int exact)
    ///   - L&amp;B only: numberOfFloors (int exact), landArea (decimal within tolerance)
    ///
    /// Identity/key fields (CondoRegistrationNumber, TowerName, RoomNumber, PlotNumber,
    /// HouseNumber) are intentionally NOT compared here — they are part of the key.
    /// </summary>
    public static bool AttributesDiffer(
        ProjectUnit existing,
        ProjectUnit incoming,
        ProjectType projectType,
        out List<string> diffFields)
    {
        diffFields = [];

        // modelType — case-insensitive, trimmed
        var existingModel = existing.ModelType?.Trim() ?? string.Empty;
        var incomingModel = incoming.ModelType?.Trim() ?? string.Empty;
        if (!string.Equals(existingModel, incomingModel, StringComparison.OrdinalIgnoreCase))
            diffFields.Add("modelType");

        // sellingPrice
        if (!DecimalsEqual(existing.SellingPrice, incoming.SellingPrice))
            diffFields.Add("sellingPrice");

        // usableArea
        if (!DecimalsEqual(existing.UsableArea, incoming.UsableArea))
            diffFields.Add("usableArea");

        if (projectType == ProjectType.Condo)
        {
            // floor
            if (existing.Floor != incoming.Floor)
                diffFields.Add("floor");
        }
        else
        {
            // numberOfFloors
            if (existing.NumberOfFloors != incoming.NumberOfFloors)
                diffFields.Add("numberOfFloors");

            // landArea
            if (!DecimalsEqual(existing.LandArea, incoming.LandArea))
                diffFields.Add("landArea");
        }

        return diffFields.Count > 0;
    }

    // Treat null-vs-null as equal; null-vs-value as different; values within tolerance as equal.
    private static bool DecimalsEqual(decimal? a, decimal? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return Math.Abs(a.Value - b.Value) <= DecimalTolerance;
    }
}
