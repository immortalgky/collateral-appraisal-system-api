using System.Text;
using Integration.Contracts.FixedWidth;

namespace Integration.FileInterface.Format.Reappraisal;

/// <summary>
/// Writes the AS400 COLLATREV inbound interface — the fixed-width (649-char Detail) UTF-8 H/D/T file
/// that <see cref="CollatrevFileParser"/> reads. Used by the dev test-file generator to produce a
/// realistic file from real completed appraisals.
///
/// Detail records are built with the shared <see cref="FixedWidthRecordBuilder"/> (same engine as the
/// outbound writers) under <see cref="FixedWidthOverflow.TruncateAll"/> — the legacy COLLATREV behaviour
/// where every column truncates to width and numeric fields are never rejected.
/// </summary>
public sealed class CollatrevFileWriter
{
    /// <summary>Detail (D) record length per the vendor spec.</summary>
    public const int DetailRecordLength = 649;

    /// <summary>Header (H) and Trailer (T) record length per the vendor spec.</summary>
    public const int HeaderTrailerLength = 640;

    // Widths/alignment mirror the parser's field map. RightZero = implied-decimal (blank stays spaces).
    private static readonly FixedWidthField[] DetailFields =
    [
        new("RecordType", 1, FixedWidthAlign.Left),
        new("ReviewType", 1, FixedWidthAlign.Left),
        new("ReviewDate", 8, FixedWidthAlign.Left),
        new("CollateralId", 19, FixedWidthAlign.Right),
        new("SurveyNo", 10, FixedWidthAlign.Left),
        new("CollateralCode", 3, FixedWidthAlign.Left),
        new("CollateralCategory", 5, FixedWidthAlign.Left),
        new("CollateralName", 40, FixedWidthAlign.Left),
        new("CollateralAddress", 120, FixedWidthAlign.Left),
        new("CifNo", 19, FixedWidthAlign.Right),
        new("CifName", 20, FixedWidthAlign.Left),
        new("AoCode", 10, FixedWidthAlign.Left),
        new("AoName", 20, FixedWidthAlign.Left),
        new("TitleNo", 20, FixedWidthAlign.Left),
        new("CurrentValue", 15, FixedWidthAlign.RightZero),
        new("ValuationDate", 8, FixedWidthAlign.Left),
        new("InternalExternal", 1, FixedWidthAlign.Left),
        new("BusinessSize", 1, FixedWidthAlign.Left),
        new("BusinessSizeDesc", 20, FixedWidthAlign.Left),
        new("MortgageAmount", 15, FixedWidthAlign.RightZero),
        new("PastDueDay", 5, FixedWidthAlign.Right),
        new("ApplicationNo", 19, FixedWidthAlign.Right),
        new("FacilityCode", 3, FixedWidthAlign.Left),
        new("FacilitySequence", 19, FixedWidthAlign.Right),
        new("CpNumber", 16, FixedWidthAlign.Left),
        new("CarCode", 3, FixedWidthAlign.Left),
        new("FacilityLimit", 15, FixedWidthAlign.RightZero),
        new("FlagLessAge4Y", 1, FixedWidthAlign.Left),
        new("FlagGreaterAge4Y", 1, FixedWidthAlign.Left),
        new("CountAgeingDate", 10, FixedWidthAlign.Left),
        new("CollateralDescription", 50, FixedWidthAlign.Left),
        new("ExternalValuerName", 40, FixedWidthAlign.Left),
        new("InternalValuerName", 40, FixedWidthAlign.Left),
        new("SllOver100M", 1, FixedWidthAlign.Left),
        new("SllDescription", 50, FixedWidthAlign.Left),
        new("Stage", 1, FixedWidthAlign.Left),
        new("IBGRetail", 10, FixedWidthAlign.Left),
        new("Group", 1, FixedWidthAlign.Left),
        new("EffectiveDateAppraisal", 8, FixedWidthAlign.Left),
    ];

    // The builder's constructor validates the widths sum to DetailRecordLength (649).
    private static readonly FixedWidthRecordBuilder DetailBuilder =
        new(DetailFields, DetailRecordLength, FixedWidthOverflow.TruncateAll);

    public string BuildHeader(DateOnly effectiveDate) =>
        ("H" + effectiveDate.ToString("ddMMyyyy")).PadRight(HeaderTrailerLength);

    public string BuildTrailer(int detailCount) =>
        ("T" + detailCount.ToString().PadLeft(9)).PadRight(HeaderTrailerLength);

    public string BuildDetail(IReadOnlyDictionary<string, string?> values)
    {
        // RecordType is always 'D' regardless of the supplied map.
        var record = new Dictionary<string, string?>(values) { ["RecordType"] = "D" };
        return DetailBuilder.Build(record);
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
}
