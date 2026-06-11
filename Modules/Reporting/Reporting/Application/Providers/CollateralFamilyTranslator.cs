namespace Reporting.Application.Providers;

/// <summary>
/// Translates an internal appraisal DOMAIN family code — the value stored on
/// <c>appraisal.AppraisalProperties.PropertyType</c> (L/LB/B/U/LS/VEH/MAC/VES/LSU/LSL) — to a Thai
/// label via a <c>parameter.Parameters</c> 'CollateralType' map.
///
/// The label map is keyed by the 33-code CollateralType scheme, NOT by the domain family codes, so a
/// direct lookup on <c>PropertyType</c> always misses. This helper maps each family to a
/// representative CollateralType code first (mirroring
/// <c>AppraisalCreationService.CodeToAppraisalFamily</c>, inverted) so the lookup resolves.
///
/// KEEP IN SYNC with that source map: when a family or CollateralType code is added/remapped there,
/// update <see cref="FamilyToParamCode"/> and the IsCondoFamily/IsEquipmentFamily sets below, or the
/// new code renders its raw value and is mis-classified for unit counting.
/// </summary>
internal static class CollateralFamilyTranslator
{
    private static readonly IReadOnlyDictionary<string, string> FamilyToParamCode =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["L"]   = "01",
            ["LB"]  = "02",
            ["B"]   = "05",
            ["LSB"] = "05",  // leasehold building — no dedicated CollateralType code; show the building label
            ["U"]   = "08",
            ["LS"]  = "09",
            ["VEH"] = "10",
            ["MAC"] = "11",
            ["VES"] = "12",
            ["LSU"] = "28",
            ["LSL"] = "29",
        };

    /// <summary>Maps a domain family code to a representative CollateralType code (identity fallback).</summary>
    public static string ToParamCode(string? domainCode) =>
        domainCode is not null && FamilyToParamCode.TryGetValue(domainCode, out var p)
            ? p
            : domainCode ?? "";

    /// <summary>
    /// Translates a domain family code to its Thai label using a CollateralType label map.
    /// Returns null for a blank code; falls back to the raw code when the map has no entry.
    /// </summary>
    public static string? ToThai(string? domainCode, IReadOnlyDictionary<string, string?> paramLabelMap)
    {
        if (string.IsNullOrWhiteSpace(domainCode))
            return null;

        var paramCode = ToParamCode(domainCode);
        return paramLabelMap.TryGetValue(paramCode, out var label) && !string.IsNullOrWhiteSpace(label)
            ? label
            : domainCode;
    }

    /// <summary>Condo families (count in ยูนิต): U, LSU.</summary>
    public static bool IsCondoFamily(string? domainCode) =>
        domainCode is "U" or "LSU";

    /// <summary>Equipment families (count in เครื่อง): MAC, VEH, VES.</summary>
    public static bool IsEquipmentFamily(string? domainCode) =>
        domainCode is "MAC" or "VEH" or "VES";
}
