using System.Data;
using System.Globalization;
using System.Text.Json;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;

namespace Reporting.Application.Providers.Sections;

/// <summary>
/// Loads the Sale Adjustment Grid (SG) and Direct Comparison (DC) tables, laid out like the pricing screens
/// (rows, labels and bands verbatim from the app's th pricingAnalysis.json).
///
/// Sources (Dapper read-only), per method picked by <see cref="PricingMethodPicker"/>:
///   PricingComparableLinks + MarketComparables   comparable columns and the "ตลาด n" sub-line
///   PricingComparativeFactors (IsSelectedForScoring = 1)  the factor rows, by DisplaySequence
///   MarketComparableData                         ค่าปัจจัย of each comparable
///   ComparisonSubject                            the ทรัพย์ column (property data, like section 8)
///   PricingFactorScores                          ระดับ (ComparisonResult), ปรับ % / จำนวนเงิน, หมายเหตุ
///   PricingCalculations                          price band, second revision, totals, weight band
///   PricingFinalValues                           the final row and the Adjust Final Value block
///
/// Printed values are stored values, except the two derived cells that mirror the screen (see "Derived cells").
/// </summary>
internal static class SaleGridSectionLoader
{
    /// <summary>Comparable columns per printed table (A4 portrait; set by rendering).</summary>
    private const int ComparablesPerTable = 3;

    public static async Task<IReadOnlyList<SaleGridSection>> LoadAllAsync(
        IDbConnection connection,
        Guid appraisalId,
        Func<DateTime, DateTime> toApplicationTime,
        Func<int, IReadOnlyDictionary<string, string?>>? subjectValues = null,
        CancellationToken ct = default)
    {
        // SaleGrid and DirectComparison are separate tables; when both are selected in a group both print.
        var methods = await PricingMethodPicker.LoadAsync(connection, appraisalId, ["SaleGrid", "DirectComparison"], ct);
        if (methods.Count == 0)
            return [];

        var sections = new List<SaleGridSection>(methods.Count);
        foreach (var m in methods)
        {
            var subject = subjectValues?.Invoke(m.GroupNumber);
            sections.Add(await LoadOneAsync(connection, m, subject, toApplicationTime));
        }
        return sections;
    }

    /// <summary>One Sale Grid / Direct Comparison table. Also used for the sub-analyses (PricingSubAnalysisLoader, no subject column).</summary>
    internal static async Task<SaleGridSection> LoadOneAsync(
        IDbConnection connection,
        PickedMethod method,
        IReadOnlyDictionary<string, string?>? subject,
        Func<DateTime, DateTime> toApplicationTime,
        decimal? usableArea = null)
    {
        var isDc = string.Equals(method.MethodType, "DirectComparison", StringComparison.OrdinalIgnoreCase);
        var mp = new DynamicParameters();
        mp.Add("MethodId", method.PricingMethodId);

        var comparableIds = (await connection.QueryAsync<Guid>(
            "SELECT MarketComparableId FROM appraisal.PricingComparableLinks WHERE PricingMethodId = @MethodId ORDER BY DisplaySequence, MarketComparableId", mp)).ToList();

        var final = await PricingGridCommon.LoadFinalValueAsync(connection, method.PricingMethodId);
        var finalLabel = isDc ? "มูลค่าสุทธิ (ต่ำสุด)" : "มูลค่าสุทธิ (ถ่วงน้ำหนัก)";

        // The weighted net value as the screen shows it, not the stored FinalValue (see WeightedNetValue). Per-area
        // rates only: a lump-sum method's stored FinalValue is rounded to 1,000, which is the figure that row keeps.
        decimal? weightedNet = null;
        if (!isDc && method.UnitType?.ToLowerInvariant() is "persqwa" or "persqm")
        {
            var weighted = await connection.QueryAsync<CalcRow>(
                "SELECT TotalAdjustedValue, Weight FROM appraisal.PricingCalculations WHERE PricingMethodId = @MethodId", mp);
            weightedNet = PricingGridCommon.WeightedNetValue(weighted.Select(w => (w.TotalAdjustedValue, w.Weight)));
        }
        var finalRows = PricingGridCommon.FinalRows(final, method.UnitType, finalLabel, perUnitSuffix: true,
            PricingGridCommon.SubjectUsableArea(subject) ?? usableArea, weightedNet);

        if (comparableIds.Count == 0)
            return new SaleGridSection { GroupNumber = method.GroupNumber, GroupName = method.GroupName, Context = method.Context, IsDirectComparison = isDc, FinalRows = finalRows };

        var headers = await PricingGridCommon.MarketHeadersAsync(connection, comparableIds);

        var calcRows = (await connection.QueryAsync<CalcRow>(
            """
            SELECT pc.MarketComparableId,
                   pc.OfferingPrice, pc.AdjustOfferPricePct, pc.AdjustOfferPriceAmt,
                   pc.SellingPrice, pc.BuySellYear, pc.AdjustedPeriodPct, pc.CumulativeAdjPeriod,
                   pc.LandAreaDeficient, pc.LandPrice, pc.LandValueAdjustment,
                   pc.UsableAreaDeficient, pc.UsableAreaPrice, pc.BuildingValueAdjustment,
                   pc.TotalFactorDiffPct, pc.TotalFactorDiffAmt, pc.TotalAdjustedValue,
                   pc.Weight, pc.WeightedAdjustedValue
            FROM appraisal.PricingCalculations pc
            WHERE pc.PricingMethodId = @MethodId
            """, mp)).GroupBy(r => r.MarketComparableId).ToDictionary(g => g.Key, g => g.First());

        // The scoring factors, in screen order.
        var factors = (await connection.QueryAsync<CompFactorRow>(
            """
            SELECT pcf.FactorId, pcf.DisplaySequence, mcf.FactorCode, mcf.DataType, mcf.ParameterGroup,
                   COALESCE(th.FactorName, en.FactorName, mcf.FieldName) AS FactorName
            FROM appraisal.PricingComparativeFactors pcf
            JOIN appraisal.MarketComparableFactors mcf ON mcf.Id = pcf.FactorId
            LEFT JOIN appraisal.MarketComparableFactorTranslations th ON th.MarketComparableFactorId = mcf.Id AND th.Language = 'th'
            LEFT JOIN appraisal.MarketComparableFactorTranslations en ON en.MarketComparableFactorId = mcf.Id AND en.Language = 'en'
            WHERE pcf.PricingMethodId = @MethodId AND pcf.IsSelectedForScoring = 1
            ORDER BY pcf.DisplaySequence, pcf.FactorId
            """, mp)).ToList();

        var eavRows = (await connection.QueryAsync<EavRow>(
            "SELECT FactorId, MarketComparableId, Value FROM appraisal.MarketComparableData WHERE MarketComparableId IN @Ids",
            new { Ids = comparableIds })).ToList();

        var scores = (await connection.QueryAsync<FactorScoreRow>(
            """
            SELECT MarketComparableId, FactorId, DisplaySequence, AdjustmentPct, AdjustmentAmt, ComparisonResult, Remarks
            FROM appraisal.PricingFactorScores
            WHERE PricingMethodId = @MethodId AND MarketComparableId IS NOT NULL
            ORDER BY DisplaySequence, MarketComparableId
            """, mp)).ToList();

        // Which halves of "ปรับปรุงครั้งที่ 2" show: the group's property types (screen: deriveSecondRevisionVisibility).
        var types = (await connection.QueryAsync<string>(
            """
            SELECT ap.PropertyType
            FROM appraisal.PropertyGroupItems pgi
            JOIN appraisal.AppraisalProperties ap ON ap.Id = pgi.AppraisalPropertyId
            WHERE pgi.PropertyGroupId = @GroupId
            """, new { GroupId = method.PropertyGroupId })).ToList();

        var paramGroups = factors.Where(f => IsParameterBacked(f.DataType) && !string.IsNullOrWhiteSpace(f.ParameterGroup))
            .Select(f => f.ParameterGroup!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var paramMaps = await ComparisonSectionLoader.LoadParameterMapsAsync(connection, paramGroups);

        var rows = BuildRows(isDc, comparableIds, calcRows, factors, eavRows, scores, paramMaps, subject,
            showLand: types.Any(t => LandTypes.Contains(t)), showBuilding: types.Any(t => BuildingTypes.Contains(t)),
            finalValue: PricingGridCommon.Money(weightedNet ?? final?.FinalValue), finalLabel: finalLabel,
            toApplicationTime: toApplicationTime, screenWeighted: weightedNet is not null, unitType: method.UnitType);

        return new SaleGridSection
        {
            GroupNumber        = method.GroupNumber,
            GroupName          = method.GroupName,
            Context            = method.Context,
            IsDirectComparison = isDc,
            ComparableHeaders  = headers,
            Chunks             = PricingGridCommon.Chunks(comparableIds.Count, ComparablesPerTable),
            Rows               = rows,
            AdjustStart        = rows.ToList().FindIndex(r => r.Kind == "band" && r.Label == AdjustBand),
            FinalRows          = finalRows
        };
    }

    /// <summary>Title of the band that starts the second table (its own header: หมายเหตุ / ปรับ % / จำนวนเงิน).</summary>
    private const string AdjustBand = "การปรับตามปัจจัย และสรุป";

    private static readonly HashSet<string> LandTypes = ["L", "LB", "LS", "LSL"];
    private static readonly HashSet<string> BuildingTypes = ["B", "LB", "LS", "U", "LSU", "LSB"];

    // ── Rows, in the screen's order ──────────────────────────────────────────────

    internal static IReadOnlyList<SaleGridRow> BuildRows(
        bool isDc,
        IReadOnlyList<Guid> ids,
        IReadOnlyDictionary<Guid, CalcRow> calc,
        IReadOnlyList<CompFactorRow> factors,
        IReadOnlyList<EavRow> eav,
        IReadOnlyList<FactorScoreRow> scores,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> paramMaps,
        IReadOnlyDictionary<string, string?>? subject,
        bool showLand,
        bool showBuilding,
        string? finalValue,
        string finalLabel,
        Func<DateTime, DateTime> toApplicationTime,
        bool screenWeighted = false,
        string? unitType = null)
    {
        var rows = new List<SaleGridRow>();
        var bahtUnit = PricingGridCommon.MoneyUnit(unitType);
        SaleGridRow Band(string title) => new() { Kind = "band", Label = title };
        // A per-comparable row; cell returns null where the comparable has no value.
        SaleGridRow Row(string label, Func<CalcRow, string?> cell, bool emphasis = false, string? unit = null) => new()
        {
            Label = label,
            Unit = unit,
            Values = ids.Select(id => calc.TryGetValue(id, out var c) ? cell(c) : null).ToList(),
            Emphasis = emphasis
        };
        static string? Money(decimal? v) => PricingGridCommon.Money(v);

        // ปัจจัยเปรียบเทียบ: ค่าปัจจัย / ระดับ per comparable, the collateral value from the property.
        rows.Add(Band("ปัจจัยเปรียบเทียบ"));
        foreach (var f in factors)
        {
            rows.Add(new SaleGridRow
            {
                Label = f.FactorName,
                SubjectValue = Subject(f, subject),
                DashEmpty = true,
                Values = ids.Select(id => FactorValue(f, eav.FirstOrDefault(e => e.FactorId == f.FactorId && e.MarketComparableId == id)?.Value, paramMaps, toApplicationTime)).ToList(),
                Amounts = ids.Select(id => Level(scores.FirstOrDefault(s => s.FactorId == f.FactorId && s.MarketComparableId == id)?.ComparisonResult)).ToList()
            });
        }

        // ราคาเสนอขายและการปรับตามเวลา: stored inputs, blank where not applicable (adjusted offer only with an offer price; period only with a sale price).
        rows.Add(Band("ราคาเสนอขายและการปรับตามเวลา"));
        rows.Add(Row("ราคาเสนอขาย", c => HasOffer(c) ? Money(c.OfferingPrice) : null, unit: bahtUnit));
        rows.Add(Row("การปรับแก้ราคาเสนอขาย (%)", c => HasOffer(c) ? Money(c.AdjustOfferPricePct) : null, unit: "%"));
        rows.Add(Row("ราคาเสนอขายสุทธิหลังปรับ", c => HasOffer(c) ? Money(c.AdjustOfferPriceAmt) : null, unit: bahtUnit));
        rows.Add(Row("ราคาซื้อขาย", c => HasSale(c) ? Money(c.SellingPrice) : null, unit: bahtUnit));
        rows.Add(Row("จำนวนปีนับจากวันซื้อขาย", c => HasSale(c) ? c.BuySellYear?.ToString(CultureInfo.InvariantCulture) : null, unit: "ปี"));
        rows.Add(Row("อัตราปรับตามเวลา (% ต่อปี)", c => HasSale(c) ? Money(c.AdjustedPeriodPct) : null, unit: "%"));
        rows.Add(Row("การปรับตามเวลาสะสม (%)", c => HasSale(c) ? Money(c.CumulativeAdjPeriod) : null, unit: "%"));
        rows.Add(Row("ราคาสุทธิหลังปรับ", c => Money(AdjustedValue(c)), emphasis: true, unit: bahtUnit));

        // ปรับปรุงครั้งที่ 2: land part, building part, total (the total always shows in the band).
        if (showLand || showBuilding)
        {
            rows.Add(Band("ปรับปรุงครั้งที่ 2"));
            if (showLand)
            {
                rows.Add(Row("พื้นที่ที่ดินส่วนขาด - เกิน (ตร.วา)", c => Money(c.LandAreaDeficient)));
                rows.Add(Row("ราคาที่ดิน บาท/ตร.วา", c => c.LandPrice?.ToString("#,##0.##", CultureInfo.InvariantCulture)));
                rows.Add(Row("ส่วนเพิ่ม-ลดค่าชดเชยที่ดิน (บาท)", c => Money(c.LandValueAdjustment)));
            }
            if (showBuilding)
            {
                rows.Add(Row("พื้นที่ใช้สอยส่วนขาด - เกิน (ตร.ม.)", c => Money(c.UsableAreaDeficient)));
                rows.Add(Row("ราคาพื้นที่ใช้สอย บาท/ตร.ม.", c => c.UsableAreaPrice?.ToString("#,##0.##", CultureInfo.InvariantCulture)));
                rows.Add(Row("ส่วนเพิ่ม-ลดค่าชดเชยอาคาร (บาท)", c => Money(c.BuildingValueAdjustment)));
            }
            rows.Add(Row("รวมหลังปรับครั้งที่ 2", c => Money(AfterSecondRevision(c)), emphasis: true));
        }

        // การปรับตามปัจจัย และสรุป: remark in the collateral column, ปรับ % / จำนวนเงิน per comparable.
        rows.Add(Band(AdjustBand));
        rows.Add(new SaleGridRow
        {
            Kind = "subh", Label = "ปัจจัย", SubjectValue = "หมายเหตุ",
            Values = ids.Select(_ => (string?)"ปรับ %").ToList(),
            Amounts = ids.Select(_ => (string?)"จำนวนเงิน").ToList()
        });
        foreach (var f in factors)
        {
            var own = scores.Where(s => s.FactorId == f.FactorId).ToList();
            string? Pick(Guid id, Func<FactorScoreRow, decimal?> sel) => Money(own.FirstOrDefault(s => s.MarketComparableId == id) is { } s ? sel(s) : null);
            rows.Add(new SaleGridRow
            {
                Label = f.FactorName,
                SubjectValue = own.Select(s => s.Remarks).FirstOrDefault(r => !string.IsNullOrWhiteSpace(r)),
                Values = ids.Select(id => Pick(id, s => s.AdjustmentPct)).ToList(),
                Amounts = ids.Select(id => Pick(id, s => s.AdjustmentAmt)).ToList()
            });
        }
        rows.Add(new SaleGridRow
        {
            Label = "รวมส่วนต่างจากปัจจัย (%)",
            Values = ids.Select(id => calc.TryGetValue(id, out var c) ? Money(c.TotalFactorDiffPct) : null).ToList(),
            Amounts = ids.Select(id => calc.TryGetValue(id, out var c) ? Money(c.TotalFactorDiffAmt) : null).ToList()
        });
        rows.Add(Row("มูลค่าหลังปรับรวม", c => Money(c.TotalAdjustedValue), emphasis: true));

        // ปรับน้ำหนัก: Sale Adjustment Grid only.
        if (!isDc)
        {
            rows.Add(Band("ปรับน้ำหนัก"));
            rows.Add(Row("ปัจจัยน้ำหนักความน่าเชื่อถือของข้อมูล", c => Money(c.Weight)));
            // Per-area rates: the screen's own product (each column at 2 dp from the stored weight), so the columns add up to the net.
            rows.Add(Row("มูลค่าที่ปรับแล้วถ่วงน้ำหนัก", c => Money(
                screenWeighted && c.TotalAdjustedValue.HasValue && c.Weight.HasValue
                    ? PricingGridCommon.Round2(c.TotalAdjustedValue.Value * c.Weight.Value)
                    : c.WeightedAdjustedValue)));
        }

        rows.Add(new SaleGridRow { Kind = "final", Label = finalLabel, SubjectValue = finalValue, Emphasis = true });
        return rows;
    }

    private static bool HasOffer(CalcRow c) => c.OfferingPrice is > 0m;
    private static bool HasSale(CalcRow c) => c.SellingPrice is > 0m;

    // ── Derived cells (not stored; each mirrors the screen's own formula) ──────────

    /// <summary>ราคาสุทธิหลังปรับ (price-band result row) — mirrors calcAdjustedValue / calcAdjustedValueFromSellingPrice (domain/calculateSaleAdjustmentGrid.ts, same in DC): offer price wins; null when the comparable has no price.</summary>
    internal static decimal? AdjustedValue(CalcRow c)
    {
        if (HasOffer(c))
        {
            var price = c.OfferingPrice!.Value;
            var pct = c.AdjustOfferPricePct ?? 0m;
            var amt = c.AdjustOfferPriceAmt ?? 0m;
            if (pct > 0m) return PricingGridCommon.Round2(price - price * pct / 100m);
            if (amt > 0m) return PricingGridCommon.Round2(amt);
            return PricingGridCommon.Round2(price);
        }
        if (HasSale(c))
        {
            var price = c.SellingPrice!.Value;
            return PricingGridCommon.Round2(price + price * (c.BuySellYear ?? 0) * (c.AdjustedPeriodPct ?? 0m) / 100m);
        }
        return null;
    }

    /// <summary>รวมหลังปรับครั้งที่ 2 — mirrors calcTotalSecondRevision (domain/calculateSaleAdjustmentGrid.ts): round2(adjusted + building + land).</summary>
    internal static decimal? AfterSecondRevision(CalcRow c) =>
        AdjustedValue(c) is { } a ? PricingGridCommon.Round2(a + (c.BuildingValueAdjustment ?? 0m) + (c.LandValueAdjustment ?? 0m)) : null;

    // ── Cells ────────────────────────────────────────────────────────────────────

    /// <summary>ระดับ: ComparisonResult E / I / B as the screen words it.</summary>
    internal static string? Level(string? result) => result?.Trim().ToUpperInvariant() switch
    {
        "E" => "เท่ากัน",
        "I" => "ด้อยกว่า",
        "B" => "ดีกว่า",
        _ => null
    };

    private static string? FactorValue(CompFactorRow f, string? raw,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> paramMaps, Func<DateTime, DateTime> toApplicationTime)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (string.Equals(f.DataType, "Numeric", StringComparison.OrdinalIgnoreCase)
            && decimal.TryParse(raw.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var n))
            return PricingGridCommon.Money(n);
        return ResolveValue(f.DataType, f.ParameterGroup, raw, paramMaps, toApplicationTime);
    }

    /// <summary>ทรัพย์ cell: the property value for the factor's code (ComparisonSubject); a numeric one loses its unit suffix ("912.33 ตารางวา" → 912.33).</summary>
    internal static string? Subject(CompFactorRow f, IReadOnlyDictionary<string, string?>? subject)
    {
        if (f.FactorCode is null || subject is null || !subject.TryGetValue(f.FactorCode, out var v) || string.IsNullOrWhiteSpace(v))
            return null;
        if (string.Equals(f.DataType, "Numeric", StringComparison.OrdinalIgnoreCase)
            && decimal.TryParse(v.Split(' ')[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var n))
            return PricingGridCommon.Money(n);
        return v;
    }

    // ── Value resolution helpers (mirrors ComparisonSectionLoader) ───────────────

    private static string? ResolveValue(
        string? dataType,
        string? parameterGroup,
        string? value,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> paramMaps,
        Func<DateTime, DateTime> toApplicationTime)
    {
        if (string.Equals(dataType, "Date", StringComparison.OrdinalIgnoreCase))
            return PricingGridCommon.DateFactorText(value, toApplicationTime);

        if (!IsParameterBacked(dataType) || string.IsNullOrWhiteSpace(parameterGroup))
            return value;
        // One reading of a factor value for every section: code order and the 99 "other" rule of the land loader.
        return LandSectionLoader.JsonCodesToThai(value, paramMaps.GetValueOrDefault(parameterGroup) ?? EmptyMap);
    }

    private static bool IsParameterBacked(string? dataType) => dataType is
        "Dropdown" or "Radio" or "Checkbox" or "CheckboxGroup";

    private static readonly IReadOnlyDictionary<string, string?> EmptyMap = new Dictionary<string, string?>();

    // ── Private Dapper flat DTOs ─────────────────────────────────────────────────

    internal sealed class CalcRow
    {
        public Guid     MarketComparableId      { get; init; }
        public decimal? OfferingPrice           { get; init; }
        public decimal? AdjustOfferPricePct     { get; init; }
        public decimal? AdjustOfferPriceAmt     { get; init; }
        public decimal? SellingPrice            { get; init; }
        public int?     BuySellYear             { get; init; }
        public decimal? AdjustedPeriodPct       { get; init; }
        public decimal? CumulativeAdjPeriod     { get; init; }
        public decimal? LandAreaDeficient       { get; init; }
        public decimal? LandPrice               { get; init; }
        public decimal? LandValueAdjustment     { get; init; }
        public decimal? UsableAreaDeficient     { get; init; }
        public decimal? UsableAreaPrice         { get; init; }
        public decimal? BuildingValueAdjustment { get; init; }
        public decimal? TotalFactorDiffPct      { get; init; }
        public decimal? TotalFactorDiffAmt      { get; init; }
        public decimal? TotalAdjustedValue      { get; init; }
        public decimal? Weight                  { get; init; }
        public decimal? WeightedAdjustedValue   { get; init; }
    }

    internal sealed class CompFactorRow
    {
        public Guid    FactorId        { get; init; }
        public int     DisplaySequence { get; init; }
        public string? FactorCode      { get; init; }
        public string? DataType        { get; init; }
        public string? ParameterGroup  { get; init; }
        public string? FactorName      { get; init; }
    }

    internal sealed class EavRow
    {
        public Guid    FactorId           { get; init; }
        public Guid    MarketComparableId { get; init; }
        public string? Value              { get; init; }
    }

    internal sealed class FactorScoreRow
    {
        public Guid     MarketComparableId { get; init; }
        public Guid     FactorId           { get; init; }
        public int      DisplaySequence    { get; init; }
        public decimal? AdjustmentPct      { get; init; }
        public decimal? AdjustmentAmt      { get; init; }
        public string?  ComparisonResult   { get; init; }
        public string?  Remarks            { get; init; }
    }

    private sealed class ParamRow
    {
        public string? Group       { get; init; }
        public string? Code        { get; init; }
        public string? Description { get; init; }
    }
}
