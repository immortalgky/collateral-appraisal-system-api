namespace Shared.DDD;

/// <summary>
/// Forms save a dash for "none" ("-", "–" …) in free-text fields, so a dash-only value must read as
/// absent. One definition, with the same dash set as the appraisal summary report's address
/// formatter (ThaiAddressFormatter.Stated), which the brief has to agree with.
/// </summary>
public static class PlaceholderText
{
    // Hyphen-minus, en/em dash, the minus sign, hyphens and the fullwidth forms that pasted or IME
    // text brings in.
    private static readonly char[] Dashes = ['-', '–', '—', '−', '‐', '‑', '‒', '－', '﹣'];

    /// <summary>The trimmed value, or null when it is blank or only dash placeholders.</summary>
    public static string? Stated(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) || trimmed.All(c => char.IsWhiteSpace(c) || Dashes.Contains(c))
            ? null
            : trimmed;
    }

    /// <summary>The value with leading and trailing dash placeholders removed ("0 -" becomes "0").</summary>
    private static string TrimDashes(string value) => value.Trim(Dashes);

    /// <summary>
    /// <see cref="Stated"/>, and also null when the value is a numeric zero ("0", "–0", "0 -"): a free-text
    /// number such as a condo floor, where the form cannot tell 0 from "not entered". Non-numeric text
    /// ("G") counts as stated. The text returned is the trimmed value as it is, dashes included.
    /// </summary>
    public static string? StatedUnlessZero(string? value) =>
        Stated(value) is { } stated
        && !(decimal.TryParse(TrimDashes(stated).Trim(), System.Globalization.NumberStyles.Any,
                 System.Globalization.CultureInfo.InvariantCulture, out var n) && n == 0m)
            ? stated
            : null;
}
