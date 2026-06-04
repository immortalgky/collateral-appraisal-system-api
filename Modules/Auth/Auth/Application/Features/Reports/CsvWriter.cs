using System.Text;

namespace Auth.Application.Features.Reports;

/// <summary>
/// Minimal RFC 4180-compliant CSV builder.
/// Fields containing commas, double-quotes, or newlines are quoted.
/// Double-quotes inside a field are escaped by doubling them.
/// </summary>
internal static class CsvWriter
{
    /// <summary>
    /// Serialises a header row followed by data rows into a UTF-8 byte array
    /// with a leading BOM so that Excel opens the file correctly.
    /// </summary>
    public static byte[] Write(IEnumerable<string> headers, IEnumerable<IEnumerable<string?>> rows)
    {
        var sb = new StringBuilder();

        sb.AppendLine(BuildRow(headers));

        foreach (var row in rows)
            sb.AppendLine(BuildRow(row));

        // UTF-8 BOM for Excel compatibility
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string BuildRow(IEnumerable<string?> fields)
        => string.Join(",", fields.Select(EscapeField));

    /// <summary>
    /// Wraps a field in double-quotes if it contains a comma, double-quote,
    /// carriage-return, or newline. Internal double-quotes are doubled.
    /// Injection-safe: fields whose first character could trigger a spreadsheet
    /// formula (=, +, -, @, tab, CR) are prefixed with a single apostrophe so
    /// the cell is treated as plain text by Excel/LibreOffice.
    /// </summary>
    private static string EscapeField(string? value)
    {
        if (value is null) return string.Empty;

        // Neutralise CSV injection: prefix with apostrophe when the first
        // character is a formula-trigger character.
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            value = "'" + value;

        var needsQuoting = value.Contains(',')
                           || value.Contains('"')
                           || value.Contains('\r')
                           || value.Contains('\n');

        if (!needsQuoting) return value;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
