using System.Text;

namespace Request.Infrastructure.Reappraisal;

/// <summary>
/// Writes the AS400 COLLATREV inbound interface — the fixed-width (660-char Detail) UTF-8 H/D/T file
/// that <see cref="CollatrevFileParser"/> reads. Used by the dev test-file generator to produce a
/// realistic file from real completed appraisals.
///
/// The field layout below mirrors the parser's documented map (and the legacy Python generator) and is
/// the single source of truth for emitted records. The writer↔parser round-trip test pins them together.
///
/// Padding: alpha fields are left-justified, numeric fields right-justified, both space-padded.
/// Over-long values are truncated to keep column alignment (same as the parser's lenient slicing).
/// </summary>
public sealed class CollatrevFileWriter
{
    public const int RecordLength = 660;

    private enum Align { Left, Right }

    private sealed record Field(string Name, int Width, Align Align);

    // Ordered Detail-record field map. Widths must sum to RecordLength (660).
    private static readonly Field[] DetailFields =
    [
        new("RecordType", 1, Align.Left),
        new("ReviewType", 1, Align.Left),
        new("ReviewDate", 8, Align.Left),   // DDMMYYYY
        new("CollateralId", 19, Align.Right),
        new("SurveyNo", 10, Align.Left),    // = AppraisalNumber
        new("CollateralCode", 3, Align.Left),
        new("CollateralCategory", 3, Align.Left),
        new("CollateralName", 30, Align.Left),
        new("CollateralAddress", 100, Align.Left),
        new("CifNo", 19, Align.Right),
        new("CifName", 20, Align.Left),
        new("AoCode", 10, Align.Left),
        new("AoName", 40, Align.Left),
        new("TitleNo", 20, Align.Left),
        new("CurrentValue", 16, Align.Right),   // dec15,2
        new("ValuationDate", 8, Align.Left),    // DDMMYYYY
        new("InternalExternal", 1, Align.Left),
        new("BusinessSize", 1, Align.Left),
        new("BusinessSizeDesc", 40, Align.Left),
        new("MortgageAmount", 16, Align.Right),
        new("PastDueDay", 5, Align.Right),
        new("ApplicationNo", 19, Align.Right),
        new("FacilityCode", 3, Align.Left),
        new("FacilitySequence", 19, Align.Right),
        new("CpNumber", 16, Align.Left),
        new("CarCode", 3, Align.Left),
        new("FacilityLimit", 16, Align.Right),
        new("FlagLessAge4Y", 1, Align.Left),
        new("FlagGreaterAge4Y", 1, Align.Left),
        new("CountAgeingDate", 10, Align.Left),
        new("CollateralDescription", 50, Align.Left),
        new("ExternalValuerName", 40, Align.Left),
        new("InternalValuerName", 40, Align.Left),
        new("SllOver100M", 1, Align.Left),
        new("SllDescription", 50, Align.Left),
        // Trailing extension fields (pos 641–660).
        new("Stage", 1, Align.Left),
        new("IBGRetail", 10, Align.Left),
        new("Group", 1, Align.Left),
        new("EffectiveDateAppraisal", 8, Align.Left),   // DDMMYYYY
    ];

    static CollatrevFileWriter()
    {
        var sum = DetailFields.Sum(f => f.Width);
        if (sum != RecordLength)
            throw new InvalidOperationException(
                $"COLLATREV detail field widths sum to {sum}, expected {RecordLength}.");
    }

    /// <summary>Header (H) record: pos 1 = 'H', pos 2–9 = EffectiveDate (DDMMYYYY), rest filler.</summary>
    public string BuildHeader(DateOnly effectiveDate) =>
        ("H" + effectiveDate.ToString("ddMMyyyy")).PadRight(RecordLength);

    /// <summary>Trailer (T) record: pos 1 = 'T', pos 2–10 = 9-char detail count (right-aligned), rest filler.</summary>
    public string BuildTrailer(int detailCount) =>
        ("T" + detailCount.ToString().PadLeft(9)).PadRight(RecordLength);

    /// <summary>
    /// Builds one Detail (D) record from a field-name → value map. Missing keys emit blank (padded) columns.
    /// RecordType is forced to 'D'.
    /// </summary>
    public string BuildDetail(IReadOnlyDictionary<string, string?> values)
    {
        var sb = new StringBuilder(RecordLength);
        foreach (var field in DetailFields)
        {
            var raw = field.Name == "RecordType"
                ? "D"
                : (values.TryGetValue(field.Name, out var v) ? v : null) ?? string.Empty;
            sb.Append(Pad(raw, field.Width, field.Align));
        }

        var line = sb.ToString();
        if (line.Length != RecordLength)
            throw new InvalidOperationException($"Built detail length {line.Length} != {RecordLength}.");
        return line;
    }

    /// <summary>Writes a full H/D/T file (UTF-8 without BOM, CRLF line endings) to <paramref name="path"/>.</summary>
    public async Task WriteFileAsync(
        string path,
        DateOnly effectiveDate,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> detailRows,
        CancellationToken cancellationToken = default)
    {
        var lines = new List<string>(detailRows.Count + 2) { BuildHeader(effectiveDate) };
        lines.AddRange(detailRows.Select(BuildDetail));
        lines.Add(BuildTrailer(detailRows.Count));

        // CRLF endings + trailing newline, matching host file conventions; parser handles both.
        var content = string.Join("\r\n", lines) + "\r\n";
        await File.WriteAllTextAsync(path, content, new UTF8Encoding(false), cancellationToken);
    }

    private static string Pad(string value, int width, Align align)
    {
        if (value.Length > width)
            value = value[..width];      // truncate to preserve column alignment
        return align == Align.Right ? value.PadLeft(width) : value.PadRight(width);
    }
}
