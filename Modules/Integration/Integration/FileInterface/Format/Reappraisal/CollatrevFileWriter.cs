using System.Text;

namespace Integration.FileInterface.Format.Reappraisal;

/// <summary>
/// Writes the AS400 COLLATREV inbound interface — the fixed-width (649-char Detail) UTF-8 H/D/T file
/// that <see cref="CollatrevFileParser"/> reads. Used by the dev test-file generator to produce a
/// realistic file from real completed appraisals.
/// </summary>
public sealed class CollatrevFileWriter
{
    /// <summary>Detail (D) record length per the vendor spec.</summary>
    public const int DetailRecordLength = 649;

    /// <summary>Header (H) and Trailer (T) record length per the vendor spec.</summary>
    public const int HeaderTrailerLength = 640;

    private enum Align
    {
        Left,
        Right,
        /// <summary>Implied-decimal: blank stays spaces; non-blank is zero-padded on the left.</summary>
        RightZero,
    }

    private sealed record Field(string Name, int Width, Align Align);

    private static readonly Field[] DetailFields =
    [
        new("RecordType", 1, Align.Left),
        new("ReviewType", 1, Align.Left),
        new("ReviewDate", 8, Align.Left),
        new("CollateralId", 19, Align.Right),
        new("SurveyNo", 10, Align.Left),
        new("CollateralCode", 3, Align.Left),
        new("CollateralCategory", 5, Align.Left),
        new("CollateralName", 40, Align.Left),
        new("CollateralAddress", 120, Align.Left),
        new("CifNo", 19, Align.Right),
        new("CifName", 20, Align.Left),
        new("AoCode", 10, Align.Left),
        new("AoName", 20, Align.Left),
        new("TitleNo", 20, Align.Left),
        new("CurrentValue", 15, Align.RightZero),
        new("ValuationDate", 8, Align.Left),
        new("InternalExternal", 1, Align.Left),
        new("BusinessSize", 1, Align.Left),
        new("BusinessSizeDesc", 20, Align.Left),
        new("MortgageAmount", 15, Align.RightZero),
        new("PastDueDay", 5, Align.Right),
        new("ApplicationNo", 19, Align.Right),
        new("FacilityCode", 3, Align.Left),
        new("FacilitySequence", 19, Align.Right),
        new("CpNumber", 16, Align.Left),
        new("CarCode", 3, Align.Left),
        new("FacilityLimit", 15, Align.RightZero),
        new("FlagLessAge4Y", 1, Align.Left),
        new("FlagGreaterAge4Y", 1, Align.Left),
        new("CountAgeingDate", 10, Align.Left),
        new("CollateralDescription", 50, Align.Left),
        new("ExternalValuerName", 40, Align.Left),
        new("InternalValuerName", 40, Align.Left),
        new("SllOver100M", 1, Align.Left),
        new("SllDescription", 50, Align.Left),
        new("Stage", 1, Align.Left),
        new("IBGRetail", 10, Align.Left),
        new("Group", 1, Align.Left),
        new("EffectiveDateAppraisal", 8, Align.Left),
    ];

    static CollatrevFileWriter()
    {
        var sum = DetailFields.Sum(f => f.Width);
        if (sum != DetailRecordLength)
            throw new InvalidOperationException(
                $"COLLATREV detail field widths sum to {sum}, expected {DetailRecordLength}.");
    }

    public string BuildHeader(DateOnly effectiveDate) =>
        ("H" + effectiveDate.ToString("ddMMyyyy")).PadRight(HeaderTrailerLength);

    public string BuildTrailer(int detailCount) =>
        ("T" + detailCount.ToString().PadLeft(9)).PadRight(HeaderTrailerLength);

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

    public async Task WriteFileAsync(
        string path,
        DateOnly effectiveDate,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> detailRows,
        CancellationToken cancellationToken = default)
    {
        var lines = new List<string>(detailRows.Count + 2) { BuildHeader(effectiveDate) };
        lines.AddRange(detailRows.Select(BuildDetail));
        lines.Add(BuildTrailer(detailRows.Count));

        var content = string.Join("\r\n", lines) + "\r\n";
        await File.WriteAllTextAsync(path, content, new UTF8Encoding(false), cancellationToken);
    }

    private static string Pad(string value, int width, Align align)
    {
        if (align == Align.RightZero)
        {
            if (string.IsNullOrEmpty(value))
                return new string(' ', width);

            if (value.Length > width)
                value = value[..width];

            return value.PadLeft(width, '0');
        }

        if (value.Length > width)
            value = value[..width];
        return align == Align.Right ? value.PadLeft(width) : value.PadRight(width);
    }
}
