using System.Data;
using System.Text.Json;
using Reporting.Application.Models.Sections;

namespace Reporting.Application.Providers.Sections;

/// <summary>
/// Loads the "ตารางวิเคราะห์มูลค่าทรัพย์สิน (โดยวิธีเปรียบเทียบข้อมูลตลาด)"
/// (Sale Grid / Direct Comparison) analysis table — FSD §2.1.2.10.
///
/// The FSD table is transposed (factors/lines as rows, comparables + a subject SP column
/// as columns). It has four blocks, top to bottom:
///   1. Collateral-detail block  — descriptive rows (ทำเล / ชื่อโครงการ / รูปแบบอาคาร /
///      ทำเลแปลง / สภาพอาคาร / เนื้อที่ดิน / พื้นที่อาคาร). Comparable cells come from the
///      MarketComparable EAV data; the SP cell from PricingComparativeFactors.CollateralValue.
///   2. Price/area adjustment block — from appraisal.PricingCalculations (one row per comparable).
///   3. Per-factor adjustment block — scoring factors with real Thai names + AdjustmentPct.
///   4. Summary block — สรุปมูลค่าทรัพย์สิน (บาท) and (ปัดเศษ), SP = MethodValue / rounded.
///
/// Navigation chain (Dapper read-only, no EF tracking):
///   appraisal.PropertyGroups (AppraisalId) → PricingAnalysis (SubjectType=0, AnchorId)
///   → PricingAnalysisApproaches → PricingAnalysisMethods (MethodType IN ('SaleGrid','DirectComparison'))
///
/// Returns <see langword="null"/> when the appraisal has no SaleGrid or DirectComparison method.
/// </summary>
internal static class SaleGridSectionLoader
{
    /// <summary>
    /// FSD §2.1.2.10 collateral-detail block: MarketComparableFactor codes in display order.
    /// Codes confirmed against Database/Migration/Scripts/20260317002400_SeedData_MarketComparable.sql.
    /// Only codes that actually have data for the linked comparables produce a row.
    /// </summary>
    private static readonly string[] DetailFactorCodes =
        ["16", "17", "45", "43", "10", "01", "13"];

    public static async Task<IReadOnlyList<SaleGridSection>> LoadAllAsync(
        IDbConnection connection,
        Guid appraisalId,
        CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("AppraisalId", appraisalId);

        // ── Q1: Resolve all SaleGrid / DirectComparison methods (one per group) ───
        // pg.GroupNumber and pg.GroupName included for the กลุ่มที่ N bar.
        // ORDER BY pg.GroupNumber, paa.Id, pam.Id — stable group order; first method
        // per group taken in C# after grouping by PropertyGroupId.
        const string methodSql = """
            SELECT
                pg.Id           AS PropertyGroupId,
                pg.GroupNumber,
                pg.GroupName,
                pam.Id          AS MethodId,
                pam.MethodValue AS MethodValue
            FROM appraisal.PropertyGroups pg
            JOIN appraisal.PricingAnalysis pa
                ON pa.AnchorId  = pg.Id
               AND pa.SubjectType = 0
            JOIN appraisal.PricingAnalysisApproaches paa
                ON paa.PricingAnalysisId = pa.Id
            JOIN appraisal.PricingAnalysisMethods pam
                ON pam.ApproachId = paa.Id
               AND pam.MethodType IN ('SaleGrid', 'DirectComparison')
            WHERE pg.AppraisalId = @AppraisalId
            ORDER BY pg.GroupNumber, paa.Id, pam.Id
            """;

        var allMethodRows = (await connection.QueryAsync<MethodRow>(methodSql, p)).ToList();
        if (allMethodRows.Count == 0)
            return [];

        // First method per property group (already ordered).
        var methodsPerGroup = allMethodRows
            .GroupBy(r => r.PropertyGroupId)
            .Select(g => g.First())
            .OrderBy(r => r.GroupNumber)
            .ToList();

        var sections = new List<SaleGridSection>(methodsPerGroup.Count);
        foreach (var methodRow in methodsPerGroup)
        {
            var section = await LoadOneAsync(connection, methodRow, ct);
            if (section is not null)
                sections.Add(section);
        }

        return sections;
    }

    private static async Task<SaleGridSection?> LoadOneAsync(
        IDbConnection connection,
        MethodRow methodRow,
        CancellationToken ct)
    {
        var mp = new DynamicParameters();
        mp.Add("MethodId", methodRow.MethodId);

        decimal? summaryRounded = methodRow.MethodValue.HasValue
            ? Math.Round(methodRow.MethodValue.Value / 1_000m, MidpointRounding.AwayFromZero) * 1_000m
            : null;

        // ── Q2: Comparable column headers (ordered by DisplaySequence) ────────────
        const string headersSql = """
            SELECT pcl.MarketComparableId, pcl.DisplaySequence
            FROM appraisal.PricingComparableLinks pcl
            WHERE pcl.PricingMethodId = @MethodId
            ORDER BY pcl.DisplaySequence
            """;

        var headerRows = (await connection.QueryAsync<HeaderRow>(headersSql, mp)).ToList();
        if (headerRows.Count == 0)
            return new SaleGridSection
            {
                GroupNumber         = methodRow.GroupNumber,
                GroupName           = methodRow.GroupName,
                ComparableHeaders   = [],
                Rows                = [],
                SummaryValue        = methodRow.MethodValue,
                SummaryValueRounded = summaryRounded
            };

        var orderedComparableIds = headerRows
            .OrderBy(h => h.DisplaySequence)
            .Select(h => h.MarketComparableId)
            .ToList();

        var headers = headerRows
            .OrderBy(h => h.DisplaySequence)
            .Select((h, i) => $"ข้อมูล {i + 1}")
            .ToList();

        // ── Q3: Per-comparable calculation values ─────────────────────────────────
        const string calcSql = """
            SELECT
                pc.MarketComparableId,
                pc.OfferingPrice, pc.OfferingPriceUnit,
                pc.SellingPrice, pc.SellingPriceUnit,
                pc.BuySellYear, pc.BuySellMonth,
                pc.AdjustedPeriodPct, pc.CumulativeAdjPeriod,
                pc.LandAreaDeficient, pc.LandAreaDeficientUnit, pc.LandPrice, pc.LandValueAdjustment,
                pc.UsableAreaDeficient, pc.UsableAreaDeficientUnit, pc.UsableAreaPrice, pc.BuildingValueAdjustment,
                pc.TotalFactorDiffPct, pc.TotalFactorDiffAmt,
                pc.TotalAdjustedValue, pc.WeightedAdjustedValue
            FROM appraisal.PricingCalculations pc
            WHERE pc.PricingMethodId = @MethodId
            """;

        var calcRows = (await connection.QueryAsync<CalcRow>(calcSql, mp))
            .ToDictionary(r => r.MarketComparableId);

        // ── Q4: Comparative factors (scoring flag, subject value, real name) ──────
        const string compFactorSql = """
            SELECT
                pcf.FactorId,
                pcf.DisplaySequence,
                pcf.IsSelectedForScoring,
                pcf.CollateralValue,
                mcf.FactorCode,
                mcf.DataType,
                mcf.ParameterGroup,
                COALESCE(th.FactorName, en.FactorName, mcf.FieldName) AS FactorName
            FROM appraisal.PricingComparativeFactors pcf
            JOIN appraisal.MarketComparableFactors mcf
                ON mcf.Id = pcf.FactorId
            LEFT JOIN appraisal.MarketComparableFactorTranslations th
                ON th.MarketComparableFactorId = mcf.Id AND th.Language = 'th'
            LEFT JOIN appraisal.MarketComparableFactorTranslations en
                ON en.MarketComparableFactorId = mcf.Id AND en.Language = 'en'
            WHERE pcf.PricingMethodId = @MethodId
            ORDER BY pcf.DisplaySequence
            """;

        var compFactors = (await connection.QueryAsync<CompFactorRow>(compFactorSql, mp)).ToList();
        var collateralValueByFactor = compFactors
            .GroupBy(f => f.FactorId)
            .ToDictionary(g => g.Key, g => g.First());

        // ── Q5: EAV factor values for the linked comparables (detail block) ───────
        var eavParams = new DynamicParameters();
        eavParams.Add("ComparableIds", orderedComparableIds);

        const string eavSql = """
            SELECT
                mcf.Id          AS FactorId,
                mcf.FactorCode,
                mcf.DataType,
                mcf.ParameterGroup,
                COALESCE(th.FactorName, en.FactorName, mcf.FieldName) AS FactorName,
                mcd.MarketComparableId,
                mcd.Value
            FROM appraisal.MarketComparableData mcd
            JOIN appraisal.MarketComparableFactors mcf
                ON mcf.Id = mcd.FactorId AND mcf.IsActive = 1
            LEFT JOIN appraisal.MarketComparableFactorTranslations th
                ON th.MarketComparableFactorId = mcf.Id AND th.Language = 'th'
            LEFT JOIN appraisal.MarketComparableFactorTranslations en
                ON en.MarketComparableFactorId = mcf.Id AND en.Language = 'en'
            WHERE mcd.MarketComparableId IN @ComparableIds
            """;

        var eavRows = (await connection.QueryAsync<EavRow>(eavSql, eavParams)).ToList();

        // ── Q6: Per-factor, per-comparable scoring adjustments (% block) ──────────
        const string factorScoreSql = """
            SELECT
                pfs.MarketComparableId,
                pfs.FactorId,
                pfs.DisplaySequence,
                pfs.AdjustmentPct,
                pfs.AdjustmentAmt,
                pfs.ComparisonResult
            FROM appraisal.PricingFactorScores pfs
            WHERE pfs.PricingMethodId = @MethodId
              AND pfs.MarketComparableId IS NOT NULL
            ORDER BY pfs.DisplaySequence, pfs.MarketComparableId
            """;

        var factorScores = (await connection.QueryAsync<FactorScoreRow>(factorScoreSql, mp)).ToList();

        // ── Q7: parameter maps for code → Thai resolution ─────────────────────────
        var paramGroups = eavRows
            .Where(r => !string.IsNullOrWhiteSpace(r.ParameterGroup))
            .Select(r => r.ParameterGroup!)
            .Concat(compFactors.Where(f => !string.IsNullOrWhiteSpace(f.ParameterGroup)).Select(f => f.ParameterGroup!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var paramMaps = await LoadParameterMapsAsync(connection, paramGroups);

        // ── Build rows ────────────────────────────────────────────────────────────
        var rows = new List<SaleGridRow>();
        rows.AddRange(BuildDetailRows(orderedComparableIds, eavRows, collateralValueByFactor, paramMaps));
        rows.AddRange(BuildValueRows(orderedComparableIds, calcRows));
        rows.AddRange(BuildFactorRows(orderedComparableIds, factorScores, compFactors));
        rows.AddRange(BuildSummaryRows(orderedComparableIds, calcRows, methodRow.MethodValue, summaryRounded));

        return new SaleGridSection
        {
            GroupNumber         = methodRow.GroupNumber,
            GroupName           = methodRow.GroupName,
            ComparableHeaders   = headers,
            Rows                = rows,
            SummaryValue        = methodRow.MethodValue,
            SummaryValueRounded = summaryRounded
        };
    }  // LoadOneAsync

    // ── Detail block (EAV + CollateralValue SP) ─────────────────────────────────

    private static IEnumerable<SaleGridRow> BuildDetailRows(
        IReadOnlyList<Guid> comparableIds,
        IReadOnlyList<EavRow> eavRows,
        IReadOnlyDictionary<Guid, CompFactorRow> collateralValueByFactor,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> paramMaps)
    {
        var columnIndex = comparableIds
            .Select((id, i) => (id, i))
            .ToDictionary(x => x.id, x => x.i);

        foreach (var code in DetailFactorCodes)
        {
            var factorRows = eavRows.Where(r => r.FactorCode == code).ToList();
            if (factorRows.Count == 0)
                continue;

            var first = factorRows[0];
            var values = new string?[comparableIds.Count];
            foreach (var r in factorRows)
            {
                if (columnIndex.TryGetValue(r.MarketComparableId, out var idx))
                    values[idx] = ResolveValue(r.DataType, r.ParameterGroup, r.Value, paramMaps);
            }

            // SP value: analyst-entered subject value for this factor (if any).
            string? subject = null;
            if (collateralValueByFactor.TryGetValue(first.FactorId, out var cf))
                subject = ResolveValue(cf.DataType, cf.ParameterGroup, cf.CollateralValue, paramMaps);

            yield return new SaleGridRow
            {
                Label        = first.FactorName,
                Values       = values,
                SubjectValue = subject
            };
        }
    }

    // ── Price / area adjustment block (from PricingCalculations) ─────────────────

    private static IEnumerable<SaleGridRow> BuildValueRows(
        IReadOnlyList<Guid> comparableIds,
        IReadOnlyDictionary<Guid, CalcRow> calcs)
    {
        SaleGridRow Row(string label, Func<CalcRow, string?> selector, bool emphasis = false) => new()
        {
            Label  = label,
            Values = comparableIds.Select(id => calcs.TryGetValue(id, out var c) ? selector(c) : null).ToList(),
            Emphasis = emphasis
        };

        static string? WithUnit(decimal? v, string? unit) =>
            v.HasValue ? v.Value.ToString("N2") + (string.IsNullOrWhiteSpace(unit) ? "" : $" {unit}") : null;

        yield return Row("ราคาเสนอขาย (บาท/หน่วย)", c => WithUnit(c.OfferingPrice, c.OfferingPriceUnit));
        yield return Row("ราคาคาดว่าจะขายได้ (บาท/หน่วย)", c => WithUnit(c.SellingPrice, c.SellingPriceUnit));
        yield return Row("ราคาซื้อ - ขาย (ปี/เดือน)", c =>
            (c.BuySellYear.HasValue || c.BuySellMonth.HasValue)
                ? $"{c.BuySellYear?.ToString() ?? "-"}/{c.BuySellMonth?.ToString() ?? "-"}"
                : null);
        yield return Row("ปรับระยะเวลาซื้อ - ขาย (%)", c => Fmt(c.CumulativeAdjPeriod));

        yield return Row("เนื้อที่ส่วน ขาด - เกิน ที่ดิน", c => WithUnit(c.LandAreaDeficient, c.LandAreaDeficientUnit));
        yield return Row("ราคาต่อตารางวา", c => Fmt(c.LandPrice));
        yield return Row("ส่วนชดเชยมูลค่าที่ดิน เพิ่ม - ลด (บาท)", c => Fmt(c.LandValueAdjustment));
        yield return Row("พื้นที่ส่วน ขาด - เกิน อาคาร", c => WithUnit(c.UsableAreaDeficient, c.UsableAreaDeficientUnit));
        yield return Row("ราคาต่อตารางเมตร", c => Fmt(c.UsableAreaPrice));
        yield return Row("ส่วนชดเชยมูลค่าอาคาร เพิ่ม - ลด (บาท)", c => Fmt(c.BuildingValueAdjustment));
    }

    // ── Per-factor adjustment block (scoring factors, real names) ────────────────

    private static IEnumerable<SaleGridRow> BuildFactorRows(
        IReadOnlyList<Guid> comparableIds,
        IReadOnlyList<FactorScoreRow> factorScores,
        IReadOnlyList<CompFactorRow> compFactors)
    {
        // Real Thai factor name by FactorId (from the comparative-factor join).
        var nameByFactor = compFactors
            .GroupBy(f => f.FactorId)
            .ToDictionary(g => g.Key, g => g.First().FactorName);

        var columnIndex = comparableIds
            .Select((id, i) => (id, i))
            .ToDictionary(x => x.id, x => x.i);

        // One row per factor (ordered by DisplaySequence), values = AdjustmentPct per comparable.
        var byFactor = factorScores
            .GroupBy(f => f.FactorId)
            .OrderBy(g => g.Min(x => x.DisplaySequence));

        foreach (var group in byFactor)
        {
            var values = new string?[comparableIds.Count];
            foreach (var fs in group)
            {
                if (!columnIndex.TryGetValue(fs.MarketComparableId, out var idx))
                    continue;
                values[idx] = fs.AdjustmentPct.HasValue
                    ? Fmt(fs.AdjustmentPct) + "%"
                    : Fmt(fs.AdjustmentAmt);
            }

            var label = nameByFactor.TryGetValue(group.Key, out var n) && !string.IsNullOrWhiteSpace(n)
                ? $"{n} (%)"
                : $"ปัจจัยที่ {group.Min(x => x.DisplaySequence)} (%)";

            yield return new SaleGridRow { Label = label, Values = values };
        }
    }

    // ── Summary block ───────────────────────────────────────────────────────────

    private static IEnumerable<SaleGridRow> BuildSummaryRows(
        IReadOnlyList<Guid> comparableIds,
        IReadOnlyDictionary<Guid, CalcRow> calcs,
        decimal? methodValue,
        decimal? methodValueRounded)
    {
        // รวมผลต่าง ปัจจัยที่มีผลต่อมูลค่าทรัพย์สิน (%)
        yield return new SaleGridRow
        {
            Label  = "รวมผลต่าง ปัจจัยที่มีผลต่อมูลค่าทรัพย์สิน (%)",
            Values = comparableIds
                .Select(id => calcs.TryGetValue(id, out var c) ? Fmt(c.TotalFactorDiffPct) : null)
                .ToList()
        };

        // ราคาหลังปรับแก้ปัจจัย
        yield return new SaleGridRow
        {
            Label  = "ราคาหลังปรับแก้ปัจจัย",
            Values = comparableIds
                .Select(id => calcs.TryGetValue(id, out var c) ? Fmt(c.TotalAdjustedValue) : null)
                .ToList()
        };

        // สรุปมูลค่าทรัพย์สิน (บาท) — per comparable weighted value; SP = MethodValue
        yield return new SaleGridRow
        {
            Label  = "สรุปมูลค่าทรัพย์สิน (บาท)",
            Values = comparableIds
                .Select(id => calcs.TryGetValue(id, out var c) ? Fmt(c.WeightedAdjustedValue ?? c.TotalAdjustedValue) : null)
                .ToList(),
            SubjectValue = Fmt(methodValue),
            Emphasis = true
        };

        // สรุปมูลค่าทรัพย์สิน (ปัดเศษ) — SP = rounded MethodValue
        yield return new SaleGridRow
        {
            Label  = "สรุปมูลค่าทรัพย์สิน (ปัดเศษ)",
            Values = new string?[comparableIds.Count],
            SubjectValue = Fmt(methodValueRounded),
            Emphasis = true
        };
    }

    // ── Value resolution helpers (mirrors ComparisonSectionLoader) ───────────────

    private static string? ResolveValue(
        string? dataType,
        string? parameterGroup,
        string? value,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> paramMaps)
    {
        if (!IsParameterBacked(dataType) || string.IsNullOrWhiteSpace(parameterGroup))
            return value;
        return JsonCodesToThai(value, paramMaps.GetValueOrDefault(parameterGroup) ?? EmptyMap);
    }

    private static bool IsParameterBacked(string? dataType) => dataType is
        "Dropdown" or "Radio" or "Checkbox" or "CheckboxGroup";

    private static string? Fmt(decimal? value) => value.HasValue ? value.Value.ToString("N2") : null;

    private static readonly IReadOnlyDictionary<string, string?> EmptyMap = new Dictionary<string, string?>();

    private static async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>>>
        LoadParameterMapsAsync(IDbConnection connection, IReadOnlyList<string> groups)
    {
        if (groups.Count == 0)
            return new Dictionary<string, IReadOnlyDictionary<string, string?>>();

        var p = new DynamicParameters();
        p.Add("Groups", groups);

        const string sql = """
            SELECT [group] AS [Group], [code] AS Code, [description] AS Description
            FROM parameter.Parameters
            WHERE [language] = 'TH' AND [isactive] = 1
              AND [group] IN @Groups
            """;

        var paramRows = (await connection.QueryAsync<ParamRow>(sql, p)).ToList();

        return paramRows
            .Where(r => !string.IsNullOrWhiteSpace(r.Group) && !string.IsNullOrWhiteSpace(r.Code))
            .GroupBy(r => r.Group!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<string, string?>)g
                    .GroupBy(x => x.Code!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => x.First().Description, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
    }

    private static string? JsonCodesToThai(string? raw, IReadOnlyDictionary<string, string?> map)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var s = raw.Trim();
        List<string> codes;
        if (s.StartsWith('['))
        {
            try { codes = JsonSerializer.Deserialize<List<string>>(s) ?? []; }
            catch (JsonException) { codes = [s]; }
        }
        else
        {
            codes = [s];
        }

        var labels = codes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => map.TryGetValue(c, out var d) && !string.IsNullOrWhiteSpace(d) ? d! : c);

        var joined = string.Join(", ", labels);
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }

    // ── Private Dapper flat DTOs ─────────────────────────────────────────────────

    private sealed class MethodRow
    {
        public Guid     PropertyGroupId { get; init; }
        public int      GroupNumber     { get; init; }
        public string?  GroupName       { get; init; }
        public Guid     MethodId        { get; init; }
        public decimal? MethodValue     { get; init; }
    }

    private sealed class HeaderRow
    {
        public Guid MarketComparableId { get; init; }
        public int  DisplaySequence    { get; init; }
    }

    private sealed class CalcRow
    {
        public Guid     MarketComparableId      { get; init; }
        public decimal? OfferingPrice           { get; init; }
        public string?  OfferingPriceUnit       { get; init; }
        public decimal? SellingPrice            { get; init; }
        public string?  SellingPriceUnit        { get; init; }
        public int?     BuySellYear             { get; init; }
        public int?     BuySellMonth            { get; init; }
        public decimal? AdjustedPeriodPct       { get; init; }
        public decimal? CumulativeAdjPeriod     { get; init; }
        public decimal? LandAreaDeficient       { get; init; }
        public string?  LandAreaDeficientUnit   { get; init; }
        public decimal? LandPrice               { get; init; }
        public decimal? LandValueAdjustment     { get; init; }
        public decimal? UsableAreaDeficient     { get; init; }
        public string?  UsableAreaDeficientUnit { get; init; }
        public decimal? UsableAreaPrice         { get; init; }
        public decimal? BuildingValueAdjustment { get; init; }
        public decimal? TotalFactorDiffPct      { get; init; }
        public decimal? TotalFactorDiffAmt      { get; init; }
        public decimal? TotalAdjustedValue      { get; init; }
        public decimal? WeightedAdjustedValue   { get; init; }
    }

    private sealed class CompFactorRow
    {
        public Guid    FactorId            { get; init; }
        public int     DisplaySequence     { get; init; }
        public bool    IsSelectedForScoring { get; init; }
        public string? CollateralValue     { get; init; }
        public string? FactorCode          { get; init; }
        public string? DataType            { get; init; }
        public string? ParameterGroup      { get; init; }
        public string? FactorName          { get; init; }
    }

    private sealed class EavRow
    {
        public Guid    FactorId           { get; init; }
        public string? FactorCode         { get; init; }
        public string? DataType           { get; init; }
        public string? ParameterGroup     { get; init; }
        public string? FactorName         { get; init; }
        public Guid    MarketComparableId { get; init; }
        public string? Value              { get; init; }
    }

    private sealed class FactorScoreRow
    {
        public Guid     MarketComparableId { get; init; }
        public Guid     FactorId           { get; init; }
        public int      DisplaySequence    { get; init; }
        public decimal? AdjustmentPct      { get; init; }
        public decimal? AdjustmentAmt      { get; init; }
        public string?  ComparisonResult   { get; init; }
    }

    private sealed class ParamRow
    {
        public string? Group       { get; init; }
        public string? Code        { get; init; }
        public string? Description { get; init; }
    }
}
