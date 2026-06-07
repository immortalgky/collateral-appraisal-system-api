using System.Data;
using Reporting.Application.Models.Sections;

namespace Reporting.Application.Providers.Sections;

/// <summary>
/// Loads the "ตารางการปรับมูลค่าหลักทรัพย์ (วิธีปรับเปรียบเทียบข้อมูลตลาด)"
/// (Sale Grid / Direct Comparison) section model — FSD §2.1.2.10.
///
/// Navigation chain (Dapper read-only, no EF tracking):
///   appraisal.PropertyGroups           (AppraisalId → PropertyGroup.Id = AnchorId)
///   → appraisal.PricingAnalysis        (SubjectType=0, AnchorId=PropertyGroup.Id)
///   → appraisal.PricingAnalysisApproaches
///   → appraisal.PricingAnalysisMethods (MethodType IN ('SaleGrid','DirectComparison'))
///
/// Per method:
///   Q2  appraisal.PricingComparableLinks  → comparable column headers (DisplaySequence order)
///   Q3  appraisal.PricingCalculations     → per-comparable adjustment values
///   Q4  appraisal.PricingFactorScores     → per-factor, per-comparable adjustment rows
///       (WHERE MarketComparableId IS NOT NULL — collateral scores not shown in grid)
///
/// Column sourcing:
///   OfferingPrice            ← PricingCalculations.OfferingPrice            [sourced]
///   OfferingPriceUnit        ← PricingCalculations.OfferingPriceUnit        [sourced]
///   AdjustOfferPricePct      ← PricingCalculations.AdjustOfferPricePct      [sourced]
///   AdjustOfferPriceAmt      ← PricingCalculations.AdjustOfferPriceAmt      [sourced]
///   SellingPrice             ← PricingCalculations.SellingPrice              [sourced]
///   BuySellYear              ← PricingCalculations.BuySellYear               [sourced]
///   BuySellMonth             ← PricingCalculations.BuySellMonth              [sourced]
///   AdjustedPeriodPct        ← PricingCalculations.AdjustedPeriodPct        [sourced]
///   CumulativeAdjPeriod      ← PricingCalculations.CumulativeAdjPeriod      [sourced]
///   LandAreaDeficient        ← PricingCalculations.LandAreaDeficient        [sourced]
///   LandAreaDeficientUnit    ← PricingCalculations.LandAreaDeficientUnit    [sourced]
///   LandPrice                ← PricingCalculations.LandPrice                [sourced]
///   LandValueAdjustment      ← PricingCalculations.LandValueAdjustment      [sourced]
///   UsableAreaDeficient      ← PricingCalculations.UsableAreaDeficient      [sourced]
///   UsableAreaDeficientUnit  ← PricingCalculations.UsableAreaDeficientUnit  [sourced]
///   UsableAreaPrice          ← PricingCalculations.UsableAreaPrice          [sourced]
///   BuildingValueAdjustment  ← PricingCalculations.BuildingValueAdjustment  [sourced]
///   TotalFactorDiffPct       ← PricingCalculations.TotalFactorDiffPct       [sourced]
///   TotalFactorDiffAmt       ← PricingCalculations.TotalFactorDiffAmt       [sourced]
///   TotalAdjustedValue       ← PricingCalculations.TotalAdjustedValue       [sourced]
///   Weight                   ← PricingCalculations.Weight                   [sourced]
///   WeightedAdjustedValue    ← PricingCalculations.WeightedAdjustedValue    [sourced]
///   SummaryValue             ← PricingAnalysisMethods.MethodValue            [sourced]
///   SummaryValueRounded      ← MethodValue rounded to nearest 1 000 (server-derived) [no DB column]
///   Factor label             ← no ComparativeFactors.FactorName column in scope;
///                              PricingFactorScores carries FactorId (Guid) only;
///                              factor label rendered as ordinal "ปัจจัยที่ {seq}" [// no source]
///
/// Returns <see langword="null"/> when the appraisal has no SaleGrid or DirectComparison method,
/// so the caller can omit the section from the rendered template.
/// </summary>
internal static class SaleGridSectionLoader
{
    /// <summary>
    /// Loads the <see cref="SaleGridSection"/> for the given <paramref name="appraisalId"/>.
    /// Returns <see langword="null"/> when no SaleGrid or DirectComparison method exists.
    /// </summary>
    /// <param name="connection">An open Dapper <see cref="IDbConnection"/>.</param>
    /// <param name="appraisalId">The appraisal to load the sale-grid section for.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<SaleGridSection?> LoadAsync(
        IDbConnection connection,
        Guid appraisalId,
        CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("AppraisalId", appraisalId);

        // ── Q1: Resolve the first SaleGrid or DirectComparison method ─────────────
        // Navigation:
        //   appraisal.PropertyGroups (AppraisalId) → PricingAnalysis (SubjectType=0, AnchorId)
        //   → PricingAnalysisApproaches → PricingAnalysisMethods
        // We take the first method ordered by its approach creation order and the method Id,
        // which gives a deterministic pick when multiple comparisons exist (rare).
        // MethodValue is retrieved here so we can populate SummaryValue directly.
        const string methodSql = """
            SELECT TOP 1
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
            ORDER BY paa.Id, pam.Id
            """;

        var methodRow = await connection.QueryFirstOrDefaultAsync<MethodRow>(methodSql, p);
        if (methodRow is null)
            return null;

        var mp = new DynamicParameters();
        mp.Add("MethodId", methodRow.MethodId);

        // ── Q2: Comparable column headers (ordered by DisplaySequence) ────────────
        // Source: appraisal.PricingComparableLinks.DisplaySequence → "ข้อมูล {seq}" label.
        // The MarketComparableId is the join key used in Q3/Q4 to align values to columns.
        const string headersSql = """
            SELECT
                pcl.MarketComparableId,
                pcl.DisplaySequence
            FROM appraisal.PricingComparableLinks pcl
            WHERE pcl.PricingMethodId = @MethodId
            ORDER BY pcl.DisplaySequence
            """;

        var headerRows = (await connection.QueryAsync<HeaderRow>(headersSql, mp)).ToList();
        if (headerRows.Count == 0)
            return BuildEmpty(methodRow);

        // Build stable ordered list of comparable IDs (drives column positions)
        var orderedComparableIds = headerRows
            .OrderBy(h => h.DisplaySequence)
            .Select(h => h.MarketComparableId)
            .ToList();

        var headers = headerRows
            .OrderBy(h => h.DisplaySequence)
            .Select(h => $"ข้อมูล {h.DisplaySequence}")
            .ToList();

        // ── Q3: Calculation values (one row per comparable) ───────────────────────
        // All columns confirmed against PricingCalculationConfiguration.cs.
        // Columns without a DB source (none here) are noted in the header comment above.
        const string calcSql = """
            SELECT
                pc.MarketComparableId,
                pc.OfferingPrice,
                pc.OfferingPriceUnit,
                pc.AdjustOfferPricePct,
                pc.AdjustOfferPriceAmt,
                pc.SellingPrice,
                pc.BuySellYear,
                pc.BuySellMonth,
                pc.AdjustedPeriodPct,
                pc.CumulativeAdjPeriod,
                pc.LandAreaDeficient,
                pc.LandAreaDeficientUnit,
                pc.LandPrice,
                pc.LandValueAdjustment,
                pc.UsableAreaDeficient,
                pc.UsableAreaDeficientUnit,
                pc.UsableAreaPrice,
                pc.BuildingValueAdjustment,
                pc.TotalFactorDiffPct,
                pc.TotalFactorDiffAmt,
                pc.TotalAdjustedValue,
                pc.Weight,
                pc.WeightedAdjustedValue
            FROM appraisal.PricingCalculations pc
            WHERE pc.PricingMethodId = @MethodId
            """;

        var calcRows = (await connection.QueryAsync<CalcRow>(calcSql, mp))
            .ToDictionary(r => r.MarketComparableId);

        // ── Q4: Factor scores per comparable (for dynamic factor rows) ────────────
        // WHERE MarketComparableId IS NOT NULL — collateral scores (null) are not
        // shown as grid columns in the FSD table.
        // Confirmed columns from PricingFactorScoreConfiguration.cs:
        //   FactorId (Guid), DisplaySequence, AdjustmentPct, AdjustmentAmt, ComparisonResult.
        // Factor display name: no FactorName column on PricingFactorScores — only FactorId (Guid).
        // We use DisplaySequence as ordinal label: "ปัจจัยที่ {seq}". // no source for label
        const string factorSql = """
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

        var factorRows = (await connection.QueryAsync<FactorRow>(factorSql, mp)).ToList();

        // ── Transpose calculations into label-rows ────────────────────────────────
        var rows = BuildRows(orderedComparableIds, calcRows, factorRows);

        // ── SummaryValueRounded: MethodValue rounded to nearest 1 000 ────────────
        // No dedicated "rounded" column on PricingAnalysisMethods; derived server-side.
        decimal? summaryRounded = methodRow.MethodValue.HasValue
            ? Math.Round(methodRow.MethodValue.Value / 1_000m, MidpointRounding.AwayFromZero) * 1_000m
            : null;

        return new SaleGridSection
        {
            ComparableHeaders   = headers,
            Rows                = rows,
            SummaryValue        = methodRow.MethodValue,
            SummaryValueRounded = summaryRounded
        };
    }

    // ── Row construction ──────────────────────────────────────────────────────

    /// <summary>
    /// Transposes per-comparable <paramref name="calcs"/> and <paramref name="factors"/>
    /// into label rows ordered per FSD §2.1.2.10.
    /// </summary>
    private static IReadOnlyList<SaleGridRow> BuildRows(
        IReadOnlyList<Guid> comparableIds,
        IReadOnlyDictionary<Guid, CalcRow> calcs,
        IReadOnlyList<FactorRow> factors)
    {
        var rows = new List<SaleGridRow>();

        // Helper: build one row from a projection over each comparable column.
        SaleGridRow Row(string label, Func<CalcRow?, string?> selector)
        {
            var values = comparableIds
                .Select(id => calcs.TryGetValue(id, out var c) ? selector(c) : null)
                .ToList();
            return new SaleGridRow { Label = label, Values = values };
        }

        // 1. ราคาเสนอขาย — OfferingPrice (with unit suffix when present)
        rows.Add(Row("ราคาเสนอขาย", c =>
            c?.OfferingPrice.HasValue == true
                ? FormatDecimal(c.OfferingPrice) + (string.IsNullOrWhiteSpace(c.OfferingPriceUnit) ? "" : $" ({c.OfferingPriceUnit})")
                : null));

        // 2. ราคาที่ซื้อขาย / ปีที่ซื้อขาย — SellingPrice / BuySellYear+BuySellMonth
        rows.Add(Row("ราคาที่ซื้อขาย", c => FormatDecimal(c?.SellingPrice)));
        rows.Add(Row("ปีที่ซื้อขาย (ปี/เดือน)", c =>
            (c?.BuySellYear.HasValue == true || c?.BuySellMonth.HasValue == true)
                ? $"{c!.BuySellYear?.ToString() ?? "-"}/{c.BuySellMonth?.ToString() ?? "-"}"
                : null));

        // 3. ปรับค่าตามช่วงเวลา % / สะสม — AdjustedPeriodPct / CumulativeAdjPeriod
        rows.Add(Row("ปรับค่าตามช่วงเวลา (%)", c => FormatDecimal(c?.AdjustedPeriodPct)));
        rows.Add(Row("ค่าปรับสะสม (%)", c => FormatDecimal(c?.CumulativeAdjPeriod)));

        // 4. ส่วนขาดที่ดิน — LandAreaDeficient / LandPrice / LandValueAdjustment
        rows.Add(Row("ส่วนขาดที่ดิน", c =>
            c?.LandAreaDeficient.HasValue == true
                ? FormatDecimal(c.LandAreaDeficient) + (string.IsNullOrWhiteSpace(c.LandAreaDeficientUnit) ? "" : $" {c.LandAreaDeficientUnit}")
                : null));
        rows.Add(Row("ราคาที่ดิน (ต่อหน่วย)", c => FormatDecimal(c?.LandPrice)));
        rows.Add(Row("ค่าปรับที่ดิน", c => FormatDecimal(c?.LandValueAdjustment)));

        // 5. ส่วนขาดอาคาร — UsableAreaDeficient / UsableAreaPrice / BuildingValueAdjustment
        rows.Add(Row("ส่วนขาดอาคาร", c =>
            c?.UsableAreaDeficient.HasValue == true
                ? FormatDecimal(c.UsableAreaDeficient) + (string.IsNullOrWhiteSpace(c.UsableAreaDeficientUnit) ? "" : $" {c.UsableAreaDeficientUnit}")
                : null));
        rows.Add(Row("ราคาอาคาร (ต่อหน่วย)", c => FormatDecimal(c?.UsableAreaPrice)));
        rows.Add(Row("ค่าปรับอาคาร", c => FormatDecimal(c?.BuildingValueAdjustment)));

        // 6. ปรับปัจจัย % / จำนวนเงิน — TotalFactorDiffPct / TotalFactorDiffAmt
        rows.Add(Row("ปรับปัจจัย (%)", c => FormatDecimal(c?.TotalFactorDiffPct)));
        rows.Add(Row("ปรับปัจจัย (จำนวนเงิน)", c => FormatDecimal(c?.TotalFactorDiffAmt)));

        // 7. Dynamic factor-score rows (one row per unique DisplaySequence across all comparables)
        // Factor display name: "ปัจจัยที่ {seq}" — no FactorName column on PricingFactorScores.
        // // no source for human-readable factor label
        var factorSeqs = factors
            .Select(f => f.DisplaySequence)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        foreach (var seq in factorSeqs)
        {
            // Build a lookup: comparableId → factor row for this sequence
            var byComparable = factors
                .Where(f => f.DisplaySequence == seq)
                .ToDictionary(f => f.MarketComparableId);

            var factorValues = comparableIds.Select(id =>
            {
                if (!byComparable.TryGetValue(id, out var fr))
                    return null;
                // Show: ComparisonResult (±%) when present, else AdjustmentPct, else AdjustmentAmt
                if (!string.IsNullOrWhiteSpace(fr.ComparisonResult))
                    return fr.ComparisonResult
                           + (fr.AdjustmentPct.HasValue ? $" ({FormatDecimal(fr.AdjustmentPct)}%)" : "");
                if (fr.AdjustmentPct.HasValue)
                    return FormatDecimal(fr.AdjustmentPct) + "%";
                return FormatDecimal(fr.AdjustmentAmt);
            }).ToList();

            rows.Add(new SaleGridRow
            {
                Label  = $"ปัจจัยที่ {seq}", // no source — FactorId Guid only; ordinal used
                Values = factorValues
            });
        }

        // 8. รวมค่าปรับทั้งหมด / มูลค่าทรัพย์สิน — TotalAdjustedValue / WeightedAdjustedValue
        rows.Add(Row("รวมค่าปรับทั้งหมด", c => FormatDecimal(c?.TotalAdjustedValue)));
        rows.Add(Row("มูลค่าทรัพย์สิน (SP)", c =>
            // Prefer WeightedAdjustedValue (SaleGrid weighting); fall back to TotalAdjustedValue
            FormatDecimal(c?.WeightedAdjustedValue ?? c?.TotalAdjustedValue)));

        return rows;
    }

    /// <summary>Returns an empty section shell when a method exists but has no linked comparables.</summary>
    private static SaleGridSection BuildEmpty(MethodRow m)
    {
        decimal? summaryRounded = m.MethodValue.HasValue
            ? Math.Round(m.MethodValue.Value / 1_000m, MidpointRounding.AwayFromZero) * 1_000m
            : null;

        return new SaleGridSection
        {
            ComparableHeaders   = [],
            Rows                = [],
            SummaryValue        = m.MethodValue,
            SummaryValueRounded = summaryRounded
        };
    }

    /// <summary>Formats a nullable decimal as N2 string; returns null for null input.</summary>
    private static string? FormatDecimal(decimal? value) =>
        value.HasValue ? value.Value.ToString("N2") : null;

    // ── Private flat DTOs for Dapper mapping ─────────────────────────────────

    private sealed class MethodRow
    {
        public Guid    MethodId    { get; init; }
        public decimal? MethodValue { get; init; }
    }

    private sealed class HeaderRow
    {
        public Guid MarketComparableId { get; init; }
        public int  DisplaySequence    { get; init; }
    }

    private sealed class CalcRow
    {
        public Guid     MarketComparableId    { get; init; }
        public decimal? OfferingPrice         { get; init; }
        public string?  OfferingPriceUnit     { get; init; }
        public decimal? AdjustOfferPricePct   { get; init; }
        public decimal? AdjustOfferPriceAmt   { get; init; }
        public decimal? SellingPrice          { get; init; }
        public int?     BuySellYear           { get; init; }
        public int?     BuySellMonth          { get; init; }
        public decimal? AdjustedPeriodPct     { get; init; }
        public decimal? CumulativeAdjPeriod   { get; init; }
        public decimal? LandAreaDeficient     { get; init; }
        public string?  LandAreaDeficientUnit { get; init; }
        public decimal? LandPrice             { get; init; }
        public decimal? LandValueAdjustment   { get; init; }
        public decimal? UsableAreaDeficient   { get; init; }
        public string?  UsableAreaDeficientUnit { get; init; }
        public decimal? UsableAreaPrice       { get; init; }
        public decimal? BuildingValueAdjustment { get; init; }
        public decimal? TotalFactorDiffPct    { get; init; }
        public decimal? TotalFactorDiffAmt    { get; init; }
        public decimal? TotalAdjustedValue    { get; init; }
        public decimal? Weight                { get; init; }
        public decimal? WeightedAdjustedValue { get; init; }
    }

    private sealed class FactorRow
    {
        public Guid     MarketComparableId { get; init; }
        public Guid     FactorId           { get; init; }
        public int      DisplaySequence    { get; init; }
        public decimal? AdjustmentPct     { get; init; }
        public decimal? AdjustmentAmt     { get; init; }
        public string?  ComparisonResult  { get; init; }
    }
}
