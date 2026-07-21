using System.Text.Json;

namespace Reporting.Application.Formatting;

/// <summary>
/// Resolves parameter codes to their Thai descriptions for report rendering.
///
/// Two shapes exist in the schema: a single scalar code column, and a JSON array column backing a
/// multi-select checkbox group. Both pair with a sibling <c>{Field}Other</c> free-text column that
/// holds what the appraiser typed when they ticked "อื่นๆ".
///
/// Code <c>99</c> means "other". Wherever it appears the paired free text is shown INSTEAD of the
/// generic "อื่นๆ" description — otherwise the report says "อื่นๆ" and silently drops what the
/// appraiser actually wrote. Scalar columns already do this in SQL via
/// <c>CASE WHEN col = '99' AND NULLIF(colOther,'') IS NOT NULL THEN colOther ...</c>; this class is
/// the equivalent for the array columns, which cannot express that per-element in SQL.
///
/// Extracted from AppraisalSummaryCondoDataProvider, which was the only provider handling 99
/// correctly for arrays. Previously each provider/loader carried its own near-identical
/// <c>JsonCodesToThai</c> copy annotated "mirrors X" — the drift those comments warned about is
/// exactly how the 99 handling went missing everywhere except condo.
/// </summary>
public static class ParameterCodeFormatter
{
    /// <summary>Parameter code that means "other".</summary>
    public const string OtherCode = "99";

    /// <summary>
    /// Resolves a single code to its Thai description, falling back to the raw code when the
    /// parameter table has no active row for it (so an unmapped code is visible, not blank).
    /// </summary>
    public static string? Translate(string? code, IReadOnlyDictionary<string, string?> map)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        return map.TryGetValue(code, out var description) && !string.IsNullOrWhiteSpace(description)
            ? description
            : code;
    }

    /// <summary>
    /// Decodes a JSON array of codes (or a bare single code), resolves each to Thai, and joins with
    /// ", ". Element <c>99</c> renders <paramref name="other"/> in its original position rather than
    /// "อื่นๆ"; when <paramref name="other"/> is blank it falls back to the resolved "อื่นๆ"
    /// description so the selection is never silently lost. Free text present without 99 selected is
    /// appended, so a stray remark still surfaces. Returns null when nothing resolves.
    /// </summary>
    public static string? DecodeJsonArray(
        string? json, string? other, IReadOnlyDictionary<string, string?> map)
    {
        if (string.IsNullOrWhiteSpace(json))
            return string.IsNullOrWhiteSpace(other) ? null : other;

        List<string>? codes;
        try
        {
            codes = JsonSerializer.Deserialize<List<string>>(json);
        }
        catch (JsonException)
        {
            // Not an array — treat the raw value as one bare code.
            return json.Trim() == OtherCode && !string.IsNullOrWhiteSpace(other) ? other : json.Trim();
        }

        var trimmedOther = string.IsNullOrWhiteSpace(other) ? null : other!.Trim();

        if (codes is null or { Count: 0 })
            return trimmedOther;

        var present = codes.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        var hasOtherCode = present.Any(c => c == OtherCode);
        var labels = present
            .Select(c => c == OtherCode && trimmedOther != null ? trimmedOther : (Translate(c, map) ?? c))
            .ToList();

        if (trimmedOther != null && !hasOtherCode)
            labels.Add(trimmedOther);

        return labels.Count > 0 ? string.Join(", ", labels) : null;
    }
}
