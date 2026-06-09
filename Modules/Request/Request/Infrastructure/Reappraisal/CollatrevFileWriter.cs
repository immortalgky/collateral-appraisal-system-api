using System.Text;

namespace Request.Infrastructure.Reappraisal;

/// <summary>
/// Writes the AS400 COLLATREV inbound interface — the fixed-width (649-char Detail) UTF-8 H/D/T file
/// that <see cref="CollatrevFileParser"/> reads. Used by the dev test-file generator to produce a
/// realistic file from real completed appraisals.
///
/// The field layout below mirrors the parser's documented map (and the legacy Python generator) and is
/// the single source of truth for emitted records. The writer↔parser round-trip test pins them together.
///
/// Record lengths follow the vendor spec literally: Detail = 649 chars, Header/Trailer = 640 chars.
///
/// Padding: alpha fields are left-justified, numeric fields right-justified, both space-padded.
/// Over-long values are truncated to keep column alignment (same as the parser's lenient slicing).
/// </summary>
public sealed class CollatrevFileWriter
{
    /// <summary>Detail (D) record length per the vendor spec.</summary>
    public const int DetailRecordLength = 649;

    /// <summary>Header (H) and Trailer (T) record length per the vendor spec.</summary>
    public const int HeaderTrailerLength = 640;

    private enum Align { Left, Right }

    private sealed record Field(string Name, int Width, Align Align);

    // Ordered Detail-record field map. Widths must sum to DetailRecordLength (649).
    private static readonly Field[] DetailFields =
    [
        new("RecordType", 1, Align.Left),
        new("ReviewType", 1, Align.Left),
        new("ReviewDate", 8, Align.Left),   // DDMMYYYY
        new("CollateralId", 19, Align.Right),
        new("SurveyNo", 10, Align.Left),    // = AppraisalNumber
        new("CollateralCode", 3, Align.Left),
        new("CollateralCategory", 5, Align.Left),
        new("CollateralName", 40, Align.Left),
        new("CollateralAddress", 120, Align.Left),
        new("CifNo", 19, Align.Right),
        new("CifName", 20, Align.Left),
        new("AoCode", 10, Align.Left),
        new("AoName", 20, Align.Left),
        new("TitleNo", 20, Align.Left),
        new("CurrentValue", 15, Align.Right),   // dec15,2
        new("ValuationDate", 8, Align.Left),    // DDMMYYYY
        new("InternalExternal", 1, Align.Left),
        new("BusinessSize", 1, Align.Left),
        new("BusinessSizeDesc", 20, Align.Left),
        new("MortgageAmount", 15, Align.Right),
        new("PastDueDay", 5, Align.Right),
        new("ApplicationNo", 19, Align.Right),
        new("FacilityCode", 3, Align.Left),
        new("FacilitySequence", 19, Align.Right),
        new("CpNumber", 16, Align.Left),
        new("CarCode", 3, Align.Left),
        new("FacilityLimit", 15, Align.Right),
        new("FlagLessAge4Y", 1, Align.Left),
        new("FlagGreaterAge4Y", 1, Align.Left),
        new("CountAgeingDate", 10, Align.Left),
        new("CollateralDescription", 50, Align.Left),
        new("ExternalValuerName", 40, Align.Left),
        new("InternalValuerName", 40, Align.Left),
        new("SllOver100M", 1, Align.Left),
        new("SllDescription", 50, Align.Left),
        // Trailing extension fields (pos 630–649).
        new("Stage", 1, Align.Left),
        new("IBGRetail", 10, Align.Left),
        new("Group", 1, Align.Left),
        new("EffectiveDateAppraisal", 8, Align.Left),   // DDMMYYYY
    ];

    static CollatrevFileWriter()
    {
        var sum = DetailFields.Sum(f => f.Width);
        if (sum != DetailRecordLength)
            throw new InvalidOperationException(
                $"COLLATREV detail field widths sum to {sum}, expected {DetailRecordLength}.");
    }

    /// <summary>Header (H) record: pos 1 = 'H', pos 2–9 = EffectiveDate (DDMMYYYY), rest filler.</summary>
    public string BuildHeader(DateOnly effectiveDate) =>
        ("H" + effectiveDate.ToString("ddMMyyyy")).PadRight(HeaderTrailerLength);

    /// <summary>Trailer (T) record: pos 1 = 'T', pos 2–10 = 9-char detail count (right-aligned), rest filler.</summary>
    public string BuildTrailer(int detailCount) =>
        ("T" + detailCount.ToString().PadLeft(9)).PadRight(HeaderTrailerLength);

    /// <summary>
    /// Builds one Detail (D) record from a field-name → value map. Missing keys emit blank (padded) columns.
    /// RecordType is forced to 'D'.
    /// </summary>
    public string BuildDetail(IReadOnlyDictionary<string, string?> values)
    {
        var sb = new StringBuilder(DetailRecordLength);
        foreach (var field in DetailFields)
        {
            var raw = field.Name == "RecordType"
                ? "D"
                : (values.TryGetValue(field.Name, out var v) ? v : null) ?? string.Empty;
            sb.Append(Pad(raw, field.Width, field.Align));
        }

        var line = sb.ToString();
        if (line.Length != DetailRecordLength)
            throw new InvalidOperationException($"Built detail length {line.Length} != {DetailRecordLength}.");
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
