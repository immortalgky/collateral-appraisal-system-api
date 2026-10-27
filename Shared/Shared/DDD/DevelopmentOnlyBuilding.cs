namespace Shared.DDD;

/// <summary>
/// The rule that decides whether a Building Detail is really ส่วนพัฒนา (fences, paving, a water
/// tank …) rather than a สิ่งปลูกสร้าง. One definition, because the appraisal summary report and the
/// appraisal brief must agree on it: a building the report lists under ส่วนพัฒนา must not be drawn
/// as a house on the brief.
/// </summary>
public static class DevelopmentOnlyBuilding
{
    /// <summary>
    /// Whether a number field (the condo building number) actually carries a value. Appraisers type
    /// "-" for "none" (35 of the 232 Building Detail rows on the dev database), so a dash-only value
    /// must read as absent. Not part of the development-only rule, which does not look at numbers.
    /// </summary>
    public static bool HasHouseNumber(string? houseNumber) =>
        !string.IsNullOrWhiteSpace(houseNumber) && houseNumber.Trim().Trim('-', '–', '—').Length > 0;

    /// <summary>
    /// At least one depreciation row, and every row flagged ส่วนพัฒนา (<c>IsBuilding = false</c>).
    /// A building with NO rows is not one of these — nothing has been entered yet. A house number
    /// does not matter: only non-building rows means development, whatever the property is called.
    /// </summary>
    /// <param name="isBuildingFlags">The <c>IsBuilding</c> flag of each depreciation row of the building.</param>
    public static bool IsDevelopmentOnly(IEnumerable<bool> isBuildingFlags)
    {
        // One pass, so a lazy sequence is enumerated once: any building row rules it out, and no rows
        // at all does too.
        var any = false;
        foreach (var isBuilding in isBuildingFlags)
        {
            if (isBuilding) return false;
            any = true;
        }
        return any;
    }
}
