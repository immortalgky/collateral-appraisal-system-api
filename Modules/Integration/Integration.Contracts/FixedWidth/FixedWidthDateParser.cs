namespace Integration.Contracts.FixedWidth;

/// <summary>
/// Shared DDMMYYYY parsing for inbound AS400 fixed-width feeds.
///
/// Extracted so every inbound parser agrees on the vendor's date rules — previously each parser
/// carried its own copy, so a correction to one feed silently left the others parsing dates
/// differently.
/// </summary>
public static class FixedWidthDateParser
{
    /// <summary>
    /// Parses a required DDMMYYYY column.
    ///
    /// Always throws <see cref="FormatException"/> on bad input — including a syntactically valid
    /// but out-of-range date such as dd=32 or mm=13. That matters: the inbound jobs treat
    /// <see cref="FormatException"/> as "permanently bad data, quarantine this file" and any other
    /// exception as "transient, retry next run", so an <see cref="ArgumentOutOfRangeException"/>
    /// leaking out would cause the same unparseable file to be retried forever.
    /// </summary>
    public static DateOnly ParseDdmmyyyy(string s, string fieldName)
    {
        var v = s.Trim();
        if (v.Length < 8 || v == "00000000")
            throw new FormatException($"Invalid {fieldName}: '{v}'");

        if (!int.TryParse(v[..2], out var dd) ||
            !int.TryParse(v[2..4], out var mm) ||
            !int.TryParse(v[4..8], out var yyyy))
            throw new FormatException($"Invalid {fieldName}: '{v}'");

        try
        {
            return new DateOnly(yyyy, mm, dd);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new FormatException($"Invalid {fieldName}: '{v}'");
        }
    }

    /// <summary>
    /// Parses an optional DDMMYYYY column. Blank, all-spaces, "00000000", a zero year, or an
    /// out-of-range date all yield null — an absent optional date is not an error.
    /// </summary>
    public static DateOnly? ParseDdmmyyyyOrNull(string s)
    {
        var v = s.Trim();
        if (v.Length < 8 || v == "00000000" || v.All(c => c == ' '))
            return null;

        if (!int.TryParse(v[..2], out var dd) ||
            !int.TryParse(v[2..4], out var mm) ||
            !int.TryParse(v[4..8], out var yyyy) ||
            yyyy == 0)
            return null;

        try { return new DateOnly(yyyy, mm, dd); }
        catch { return null; }
    }
}
