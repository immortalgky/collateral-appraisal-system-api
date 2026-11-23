using Reporting.Application.Formatting;
using System.Data;
using System.Globalization;
using Reporting.Application.Models.Sections;

namespace Reporting.Application.Providers.Sections;

/// <summary>What the WQS and Sale Grid / Direct Comparison loaders share: number formats, column chunking,
/// the "ตลาด n" headers and the Adjust Final Value block.</summary>
internal static class PricingGridCommon
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Money and percentages: 2 decimals.</summary>
    public static string? Money(decimal? v) => v?.ToString("#,##0.00", Inv);

    /// <summary>Weights, intensities, scores: no decimals when whole.</summary>
    public static string? Whole(decimal? v) => v?.ToString("#,##0.##", Inv);

    /// <summary>The screen's round2 (Math.round on a positive number).</summary>
    public static decimal Round2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    /// <summary>Splits <paramref name="count"/> comparable columns into balanced tables of at most <paramref name="max"/>.</summary>
    public static IReadOnlyList<TableChunk> Chunks(int count, int max)
    {
        if (count <= 0) return [];
        var tables = (count + max - 1) / max;
        var per = (count + tables - 1) / tables;
        return Enumerable.Range(0, tables)
            .Select(i => new TableChunk { Start = i * per, Count = Math.Min(per, count - i * per) })
            .Where(c => c.Count > 0)
            .ToList();
    }

    // ── Comparable headers ──────────────────────────────────────────────────────

    public static async Task<IReadOnlyList<MarketHeader>> MarketHeadersAsync(IDbConnection connection, IReadOnlyList<Guid> comparableIds)
    {
        if (comparableIds.Count == 0) return [];
        var rows = (await connection.QueryAsync<MarketRow>(
            "SELECT Id, SalePrice, SaleDate, InfoDateTime, OfferPriceUnit, SalePriceUnit FROM appraisal.MarketComparables WHERE Id IN @Ids",
            new { Ids = comparableIds })).ToDictionary(r => r.Id);
        return comparableIds.Select((id, i) => new MarketHeader
        {
            Title = $"ตลาด {i + 1}",
            Sub = rows.TryGetValue(id, out var r) ? MarketSub(r.SalePrice, r.SaleDate, r.InfoDateTime, r.OfferPriceUnit, r.SalePriceUnit) : null
        }).ToList();
    }

    // date, an optional time of day (hours alone are allowed), then an offset or Z: "2024-03-05+07:00", "2024-03-04T17Z", "2024-03-04 17:00:00Z".
    // A plain "2024-03-05" has nothing after its date, so it is not an offset value (its "-05" is the day).
    private static readonly System.Text.RegularExpressions.Regex IsoWithOffset = new(
        @"^\d{4}-\d{2}-\d{2}(?:[T ]\d{1,2}(?::\d{2}(?::\d{2}(?:\.\d+)?)?)?)?\s*(?:Z|[+-]\d{2}(?::?\d{2})?)$",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static readonly string[] LocalIsoFormats =
    [
        "yyyy-MM-dd", "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd HH:mm", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF", "yyyy-MM-dd HH:mm:ss.FFFFFFF"
    ];

    private static readonly string[] OffsetFormats =
    [
        "yyyy-MM-ddK", "yyyy-MM-dd'T'HHK", "yyyy-MM-dd HHK", "yyyy-MM-dd'T'HH:mmK", "yyyy-MM-dd HH:mmK",
        "yyyy-MM-dd'T'HH:mm:ssK", "yyyy-MM-dd HH:mm:ssK", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK", "yyyy-MM-dd HH:mm:ss.FFFFFFFK"
    ];

    /// <summary>
    /// A factor value whose DataType is Date, stored as ISO text, printed like every other date of the book. The day never depends on the
    /// server's time zone. A value WITHOUT an offset ("2024-03-05", "2024-03-05T23:30:00") is the calendar date as written. A value that is an
    /// instant (a "Z" or an offset: "2024-03-04T17:00:00.000Z", "2024-03-04 17:00:00Z", "2024-03-05+07:00", "2024-03-04T17Z") is read as that
    /// instant, converted to the application's time zone by <paramref name="toApplicationTime"/> (from UTC), and only then is its date taken.
    /// Any other text (not ISO, e.g. "05/03/2024") is left exactly as it was typed.
    /// </summary>
    internal static string? DateFactorText(string? value, Func<DateTime, DateTime> toApplicationTime)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;
        var text = value.Trim();

        if (IsoWithOffset.IsMatch(text))
        {
            var compact = System.Text.RegularExpressions.Regex.Replace(text, @"\s+(?=(?:Z|[+-]\d{2}(?::?\d{2})?)$)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
            if (DateTimeOffset.TryParseExact(compact, OffsetFormats, Inv, DateTimeStyles.None, out var instant)
                || DateTimeOffset.TryParse(compact, Inv, DateTimeStyles.None, out instant))
                return ThaiDateFormatter.Long(toApplicationTime(instant.UtcDateTime));
        }

        // No zone in the text: only an ISO form is read, and as the calendar date written. Anything else ("05/03/2024" can be 5 March or 3 May, a
        // Buddhist-era year is not a Gregorian one) prints as it was typed rather than guessed with the server's culture.
        return DateTime.TryParseExact(text, LocalIsoFormats, Inv, DateTimeStyles.None, out var d) ? ThaiDateFormatter.Long(d) : value;
    }

    /// <summary>Unit of the money rows in the price band: the method's UnitType (PerSqWa / PerSqm / otherwise lump sum).</summary>
    internal static string MoneyUnit(string? unitType) => unitType?.ToLowerInvariant() switch
    {
        "persqwa" => "บาท/ตร.วา",
        "persqm" => "บาท/ตร.ม.",
        _ => "บาท"
    };

    /// <summary>Mirrors the screen's marketKindLabel · marketDateLabel · marketUnitLabel.</summary>
    internal static string MarketSub(decimal? salePrice, DateTime? saleDate, DateTime? infoDate, string? offerUnit, string? saleUnit)
    {
        var kind = salePrice != null ? "ซื้อขายจริง" : "ประกาศขาย";
        var date = ThaiDateFormatter.Long(saleDate ?? infoDate) ?? "-";
        var unit = (offerUnit ?? saleUnit) switch { "PerSqWa" => "/ตร.ว.", "PerSqm" => "/ตร.ม.", _ => "เหมา" };
        return $"{kind} · {date} · {unit}";
    }

    // ── Adjust Final Value ──────────────────────────────────────────────────────

    internal sealed class FinalValueRow
    {
        public decimal? FinalValue { get; init; }
        public decimal? FinalValueOverride { get; init; }
        public decimal? LandArea { get; init; }
        public decimal? LandValue { get; init; }
        public decimal? BuildingValue { get; init; }
        public decimal? IndicatedValue { get; init; }
        public bool HasBuildingValue { get; init; }
    }

    public static Task<FinalValueRow?> LoadFinalValueAsync(IDbConnection connection, Guid methodId) =>
        connection.QueryFirstOrDefaultAsync<FinalValueRow>(
            """
            SELECT FinalValue, FinalValueOverride, LandArea, LandValue, BuildingValue, IndicatedValue, HasBuildingValue
            FROM appraisal.PricingFinalValues WHERE PricingMethodId = @methodId
            """, new { methodId });

    /// <summary>
    /// The weighted net value the screen shows: each column's total adjusted value × its stored weight at 2 dp
    /// (calcWeightedAdjustValue), summed and rounded at 2 dp (the finalValue rule). The screen recomputes it on load
    /// from the stored weights, which the column keeps to 5 dp, so it can differ by a few baht from the stored
    /// FinalValue that the save computed from the unrounded weights. Null when no column has both figures.
    /// </summary>
    internal static decimal? WeightedNetValue(IEnumerable<(decimal? TotalAdjustedValue, decimal? Weight)> columns)
    {
        var parts = columns.Where(c => c.TotalAdjustedValue.HasValue && c.Weight.HasValue)
            .Select(c => Round2(c.TotalAdjustedValue!.Value * c.Weight!.Value)).ToList();
        return parts.Count == 0 ? null : Round2(parts.Sum());
    }

    /// <summary>
    /// The block's rows, in the screen's order: net value, rate used (unit price only), area (unit price only),
    /// then มูลค่าตามวิธี (or land / building / total when it includes the building cost).
    /// "แก้เอง" = the rate used differs from the net value (unit price), or the indicated value does (lump sum).
    /// <paramref name="usableArea"/>: the per-sqm area row. Pricing does not store it; the screen shows the property's
    /// usable area (see <see cref="SubjectUsableArea"/>), so the caller passes the same figure.
    /// </summary>
    public static IReadOnlyList<PricingFinalRow> FinalRows(FinalValueRow? fv, string? unitType, string finalLabel, bool perUnitSuffix, decimal? usableArea = null, decimal? netValue = null)
    {
        if (fv is null) return [];
        var u = unitType?.ToLowerInvariant();
        var unitPrice = u is "persqwa" or "persqm";
        var area = u == "persqm" ? "ตร.ม." : "ตร.วา";

        // The net row prints netValue (recomputed from the stored weights), else the stored FinalValue; the "แก้เอง" notes compare
        // against that same printed figure, so a few-baht recompute difference never shows as an edit.
        var printedNet = netValue ?? fv.FinalValue;
        var rows = new List<PricingFinalRow>
        {
            new() { Label = unitPrice && perUnitSuffix ? $"{finalLabel} ต่อ{area}" : finalLabel, Value = Money(printedNet), Unit = "บาท" }
        };
        if (unitPrice)
        {
            rows.Add(new() { Label = $"ราคาต่อ{area}ที่ใช้", Value = Money(fv.FinalValueOverride), Unit = "บาท", Note = Edited(fv.FinalValueOverride, printedNet) });
            rows.Add(new() { Label = u == "persqm" ? "พื้นที่ใช้สอย" : "เนื้อที่ดิน", Value = Money(u == "persqm" ? usableArea : fv.LandArea), Unit = area });
        }
        if (fv.HasBuildingValue)
        {
            rows.Add(new() { Label = "มูลค่าที่ดิน", Value = Money(fv.LandValue), Unit = "บาท" });
            rows.Add(new() { Label = "ค่าอาคาร จาก Building Cost", Value = Money(fv.BuildingValue), Unit = "บาท" });
            rows.Add(new() { Label = "มูลค่าตามวิธี (รวมอาคาร)", Value = Money(fv.IndicatedValue), Unit = "บาท" });
        }
        else
        {
            rows.Add(new() { Label = "มูลค่าตามวิธี (Indicated Value)", Value = Money(fv.IndicatedValue), Unit = "บาท", Note = unitPrice ? null : Edited(fv.IndicatedValue, printedNet) });
        }
        return rows;
    }

    /// <summary>
    /// The group's usable area, as the pricing screen shows it on a per-sqm final value: the property's building
    /// area (sum of the real buildings) or the condo's usable area — the "14 พื้นที่ใช้สอย" value
    /// <see cref="ComparisonSubject"/> already resolved for the group.
    /// </summary>
    internal static decimal? SubjectUsableArea(IReadOnlyDictionary<string, string?>? subject) =>
        subject is not null && subject.TryGetValue("14", out var s)
        && decimal.TryParse(s, NumberStyles.Number, Inv, out var v) ? v : null;

    internal static string? Edited(decimal? used, decimal? computed) =>
        used.HasValue && computed.HasValue && used != computed
            ? $"แก้เอง · ต่างจากที่คำนวณ {(used.Value - computed.Value).ToString("+#,##0.00;-#,##0.00", Inv)}"
            : null;

    private sealed class MarketRow
    {
        public Guid Id { get; init; }
        public decimal? SalePrice { get; init; }
        public DateTime? SaleDate { get; init; }
        public DateTime? InfoDateTime { get; init; }
        public string? OfferPriceUnit { get; init; }
        public string? SalePriceUnit { get; init; }
    }
}
