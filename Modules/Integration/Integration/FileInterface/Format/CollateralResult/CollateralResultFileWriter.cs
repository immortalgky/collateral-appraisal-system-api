using System.Globalization;
using Collateral.Contracts.FileInterface;
using Integration.Contracts.FixedWidth;

namespace Integration.FileInterface.Format.CollateralResult;

/// <summary>
/// Writes the outbound "Collateral Result" interface — a fixed-width 198-char UTF-8 H/D/T file
/// sent to the host (AS400) to update collateral prices after an appraisal completes.
///
/// Decimal format: implied-decimal, NO decimal point — the value is multiplied by 100 and the field
/// carries the integer string, left zero-filled (scale-2 fields shrank by 1 char vs the original spec
/// which reserved a dot position). A null OR zero amount is emitted as an ALL-ZEROS numeric field
/// (AS400 zoned-decimal fields cannot hold spaces). The trailer detail-count is zero-padded.
///
/// Detail = 198 chars; Header/Trailer = 198 chars.
/// </summary>
public sealed class CollateralResultFileWriter
{
    public const int RecordLength = 198;

    // Ordered Detail-record field map. Widths must sum to RecordLength (198).
    // Verified: 1+19+10+15+15+15+15+8+8+4+40+4+40+3+1 = 198.
    private static readonly FixedWidthField[] DetailFields =
    [
        new("RecordType",             1,  FixedWidthAlign.Left),          // 'D'
        new("CollateralId",          19,  FixedWidthAlign.RightZeroFill), // pos 2-20
        new("AppraisalReportNumber", 10,  FixedWidthAlign.Left),          // pos 21-30
        new("AppraisalValue",        15,  FixedWidthAlign.RightZeroFill), // pos 31-45  implied dec(15,2)
        new("LandValue",             15,  FixedWidthAlign.RightZeroFill), // pos 46-60
        new("BuildingValue",         15,  FixedWidthAlign.RightZeroFill), // pos 61-75
        new("ForceSaleValue",        15,  FixedWidthAlign.RightZeroFill), // pos 76-90
        new("CurrentAppraisalDate",   8,  FixedWidthAlign.Left),          // pos 91-98  DDMMYYYY
        new("NextAppraisalDate",      8,  FixedWidthAlign.Left),          // pos 99-106 DDMMYYYY
        new("InternalValuerCode",     4,  FixedWidthAlign.Left),          // pos 107-110
        new("InternalValuerName",    40,  FixedWidthAlign.Left),          // pos 111-150
        new("ExternalValuerCode",     4,  FixedWidthAlign.Left),          // pos 151-154
        new("ExternalValuerName",    40,  FixedWidthAlign.Left),          // pos 155-194
        new("LifeYear",               3,  FixedWidthAlign.RightZeroFill), // pos 195-197 dec(3,0)
        new("AppraisalStatus",        1,  FixedWidthAlign.Left),          // pos 198
    ];

    private static readonly FixedWidthRecordBuilder DetailBuilder =
        new(DetailFields, RecordLength, FixedWidthOverflow.ThrowOnNumeric);

    /// <summary>Header (H): pos 1 = 'H', pos 2-9 = EffectiveDate (DDMMYYYY), rest filler.</summary>
    public string BuildHeader(DateOnly effectiveDate) =>
        ("H" + effectiveDate.ToString("ddMMyyyy", CultureInfo.InvariantCulture)).PadRight(RecordLength);

    /// <summary>Trailer (T): pos 1 = 'T', pos 2-10 = 9-char detail count (zero-filled), rest filler.</summary>
    public string BuildTrailer(int detailCount) =>
        ("T" + detailCount.ToString(CultureInfo.InvariantCulture).PadLeft(9, '0')).PadRight(RecordLength);

    /// <summary>Builds one 198-char Detail (D) record from a typed row.</summary>
    public string BuildDetail(CollateralResultRow row)
    {
        var values = new Dictionary<string, string?>
        {
            ["RecordType"] = "D",
            // CollateralId is a numeric host id (dec 19,0). Null → all zeros, not blank.
            ["CollateralId"] = row.CollateralId ?? "0",
            ["AppraisalReportNumber"] = row.AppraisalReportNumber,
            ["AppraisalValue"] = Money(row.AppraisalValue),
            ["LandValue"] = Money(row.LandValue),
            ["BuildingValue"] = Money(row.BuildingValue),
            ["ForceSaleValue"] = Money(row.ForceSaleValue),
            ["CurrentAppraisalDate"] = Date(row.CurrentAppraisalDate),
            ["NextAppraisalDate"] = Date(row.NextAppraisalDate),
            ["InternalValuerCode"] = row.InternalValuerCode,
            ["InternalValuerName"] = row.InternalValuerName,
            ["ExternalValuerCode"] = row.ExternalValuerCode,
            ["ExternalValuerName"] = row.ExternalValuerName,
            // LifeYear dec(3,0): null / out-of-range → all zeros (host requirement).
            ["LifeYear"] = (row.LifeYear is { } ly && ly >= 0 && ly <= 999 ? ly : 0)
                .ToString(CultureInfo.InvariantCulture),
            ["AppraisalStatus"] = row.AppraisalStatus,
        };

        return DetailBuilder.Build(values);
    }

    /// <summary>
    /// Builds the full H/D/T file content (UTF-8 text, CRLF line endings, trailing newline).
    /// </summary>
    public string BuildContent(DateOnly effectiveDate, IReadOnlyList<CollateralResultRow> rows)
    {
        var lines = new List<string>(rows.Count + 2) { BuildHeader(effectiveDate) };
        lines.AddRange(rows.Select(BuildDetail));
        lines.Add(BuildTrailer(rows.Count));
        return string.Join("\r\n", lines) + "\r\n";
    }

    // Money: implied-decimal scale-2 integer string (×100, no dot, no padding; builder zero-pads to width).
    // null OR zero → "0" → builder zero-fills the column. e.g. 5000000.50 → "500000050"; 100.00 → "10000".
    private static string Money(decimal? value) =>
        ((long)Math.Round((value ?? 0m) * 100m, MidpointRounding.AwayFromZero))
            .ToString(CultureInfo.InvariantCulture);

    private static string? Date(DateOnly? value) =>
        value?.ToString("ddMMyyyy", CultureInfo.InvariantCulture);
}
