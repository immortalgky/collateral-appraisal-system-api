using System.Data;
using Reporting.Application.Models.Sections;

namespace Reporting.Application.Providers.Sections;

/// <summary>
/// Loads the "WQS — Weighted Quality Score" section model — FSD §2.1.2.9.
///
/// Data sources (Dapper read-only, no EF Core tracking):
///
///   Q1  appraisal.PricingAnalysis
///         → appraisal.PricingAnalysisApproaches
///           → appraisal.PricingAnalysisMethods WHERE MethodType = 'WQS'
///       Resolves all WQS PricingMethodIds anchored to PropertyGroups
///       (SubjectType = 0) belonging to the given AppraisalId.
///       Groups by pg.Id; takes first pam.Id per group.
///       Also pulls pg.GroupNumber / pg.GroupName (for the กลุ่มที่ N bar),
///       pam.MethodValue → AppraisalValue and pam.ValuePerUnit → LandPerSqWa.
///
///   Q2  appraisal.PricingComparableLinks → the comparables in DisplaySequence order; appraisal.MarketComparables
///       → the "ตลาด n" sub-line (kind · date · unit), see PricingGridCommon.
///
///   Q3  appraisal.PricingFactorScores → the factor rows (the distinct factors, in the score rows' DisplaySequence order,
///       as the screen lists them): collateral row (MarketComparableId IS NULL) and one score row per comparable.
///       Names: MarketComparableFactorTranslations (Thai preferred).
///
///   Q4  appraisal.PricingRsqResults → regression statistics.
///   Q5  appraisal.PricingCalculations → the price band (stored inputs and TotalAdjustedValue).
///   Q6  appraisal.PricingFinalValues → the Adjust Final Value block.
///
/// Everything printed is a stored value, except the two derivations that mirror the screen
/// (WeightedIntensity and the รวม row), each in its own method below.
///
/// Returns an empty list when no WQS method exists for the appraisal.
/// When a group has multiple WQS methods the first one (lowest pam.Id via ORDER BY pam.Id)
/// is used for that group.
/// </summary>
internal static class WqsSectionLoader
{
    /// <summary>
    /// Loads all <see cref="WqsSection"/>s for the given <paramref name="appraisalId"/>,
    /// one per property group that has a WQS pricing method.
    /// Returns an empty list when the appraisal has no WQS pricing method.
    /// </summary>
    /// <param name="connection">An open Dapper <see cref="IDbConnection"/>.</param>
    /// <param name="appraisalId">The appraisal to load the WQS sections for.</param>
    /// <param name="ct">Cancellation token (unused by Dapper but signals intent).</param>
    /// <param name="subjectValues">The group's subject values (ComparisonSubject); only its usable area is read, for a per-sqm final value.</param>
    public static async Task<IReadOnlyList<WqsSection>> LoadAllAsync(
        IDbConnection connection,
        Guid appraisalId,
        CancellationToken ct = default,
        Func<int, IReadOnlyDictionary<string, string?>>? subjectValues = null)
    {
        // ── Q1: the WQS methods to print ──────────────────────────────────────────
        // PricingMethodPicker decides which: the appraiser's selected method(s) of every approach,
        // plus the analysed-but-unselected (reference) ones — never "first method by Id".
        var methodsPerGroup = await PricingMethodPicker.LoadAsync(connection, appraisalId, ["WQS"], ct);
        if (methodsPerGroup.Count == 0)
            return [];

        var sections = new List<WqsSection>(methodsPerGroup.Count);
        foreach (var method in methodsPerGroup)
        {
            var usable = PricingGridCommon.SubjectUsableArea(subjectValues?.Invoke(method.GroupNumber));
            var section = await LoadOneAsync(connection, method, ct, usable);
            if (section is not null)
                sections.Add(section);
        }

        return sections;
    }

    /// <summary>One row per (factor, comparable [null = the collateral]); the first in the given order wins.</summary>
    internal static List<ScoreRow> DistinctScoreRows(IEnumerable<ScoreRow> rows) =>
        rows.GroupBy(s => (s.FactorId, s.MarketComparableId)).Select(g => g.First()).ToList();

    /// <summary>Comparable columns per printed table (A4 portrait; set by rendering).</summary>
    private const int ComparablesPerTable = 4;

    // ── Per-method loader ──────────────────────────────────────────────────────────

    /// <summary>One WQS table. Also used for the sub-analyses (PricingSubAnalysisLoader).</summary>
    internal static async Task<WqsSection?> LoadOneAsync(
        IDbConnection connection,
        PickedMethod method,
        CancellationToken ct,
        decimal? usableArea = null)
    {
        var mp = new DynamicParameters();
        mp.Add("PricingMethodId", method.PricingMethodId);

        // Q2: comparables in column order.
        var comparableIds = (await connection.QueryAsync<Guid>(
            "SELECT MarketComparableId FROM appraisal.PricingComparableLinks WHERE PricingMethodId = @PricingMethodId ORDER BY DisplaySequence, MarketComparableId", mp)).ToList();
        var headers = await PricingGridCommon.MarketHeadersAsync(connection, comparableIds);

        // Q3: score rows (NULL MarketComparableId = collateral). A null Guid? cannot be a dictionary key, so split it out.
        var scoreRows = (await connection.QueryAsync<ScoreRow>(
            """
            SELECT FactorId, MarketComparableId, DisplaySequence, FactorWeight, Intensity, Score, WeightedScore
            FROM appraisal.PricingFactorScores
            WHERE PricingMethodId = @PricingMethodId
            ORDER BY DisplaySequence, FactorId, MarketComparableId
            """, mp)).ToList();

        // One row per (factor, comparable), taken once here, so the cells, the รวม row and the scatter's X all read the same rows:
        // a duplicate must neither throw nor be counted twice in a sum. The first by the stored order wins.
        scoreRows = DistinctScoreRows(scoreRows);

        var factorIds = scoreRows.GroupBy(s => s.FactorId).OrderBy(g => g.Min(s => s.DisplaySequence)).Select(g => g.Key).ToList();
        var names = factorIds.Count == 0
            ? new Dictionary<Guid, string?>()
            : (await connection.QueryAsync<FactorNameRow>(
                """
                SELECT mcf.Id AS FactorId, COALESCE(th.FactorName, en.FactorName, mcf.FieldName) AS FactorName
                FROM appraisal.MarketComparableFactors mcf
                LEFT JOIN appraisal.MarketComparableFactorTranslations th ON th.MarketComparableFactorId = mcf.Id AND th.Language = 'th'
                LEFT JOIN appraisal.MarketComparableFactorTranslations en ON en.MarketComparableFactorId = mcf.Id AND en.Language = 'en'
                WHERE mcf.Id IN @Ids
                """, new { Ids = factorIds })).ToDictionary(r => r.FactorId, r => r.FactorName);

        var subjectByFactor = scoreRows.Where(s => s.MarketComparableId is null).GroupBy(s => s.FactorId).ToDictionary(g => g.Key, g => g.First());
        var compScoresByFactor = scoreRows.Where(s => s.MarketComparableId is not null).GroupBy(s => s.FactorId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(s => s.MarketComparableId!.Value, s => s)); // (factor, comparable) is unique after the de-duplication above

        var factors = factorIds.Select(fid =>
        {
            subjectByFactor.TryGetValue(fid, out var subj);
            compScoresByFactor.TryGetValue(fid, out var byComp);
            ScoreRow? Comp(Guid cid) => byComp is not null && byComp.TryGetValue(cid, out var cs) ? cs : null;
            return new WqsFactorRow
            {
                FactorName         = names.GetValueOrDefault(fid),
                Weight             = PricingGridCommon.Whole(subj?.FactorWeight),
                Intensity          = PricingGridCommon.Whole(subj?.Intensity),
                WeightedIntensity  = WeightedIntensity(subj?.FactorWeight, subj?.Intensity),
                SubjectScore       = PricingGridCommon.Whole(subj?.Score),
                SubjectWeighted    = PricingGridCommon.Money(subj?.WeightedScore),
                ComparableScores   = comparableIds.Select(c => PricingGridCommon.Whole(Comp(c)?.Score)).ToList(),
                ComparableWeighted = comparableIds.Select(c => PricingGridCommon.Money(Comp(c)?.WeightedScore)).ToList(),
            };
        }).ToList();

        // Q4: regression statistics.
        var rsq = await connection.QueryFirstOrDefaultAsync<RsqRow>(
            """
            SELECT CoefficientOfDecision AS Rsq, StandardError AS Steyx, IntersectionPoint AS Intercept, Slope,
                   RsqFinalValue AS Forecast, LowestEstimate, HighestEstimate
            FROM appraisal.PricingRsqResults WHERE PricingMethodId = @PricingMethodId
            """, mp);

        // Q5: per-comparable calculation rows.
        var calcByComparable = (await connection.QueryAsync<CalcRow>(
            """
            SELECT MarketComparableId, TotalAdjustedValue, SellingPrice, BuySellYear, AdjustedPeriodPct, CumulativeAdjPeriod,
                   OfferingPrice, AdjustOfferPricePct, AdjustOfferPriceAmt
            FROM appraisal.PricingCalculations WHERE PricingMethodId = @PricingMethodId
            """, mp)).GroupBy(c => c.MarketComparableId).ToDictionary(g => g.Key, g => g.First());

        // Scatter: X = Σ stored WeightedScore per comparable, Y = TotalAdjustedValue.
        var scoreByComparable = scoreRows.Where(s => s.MarketComparableId is not null && s.WeightedScore.HasValue)
            .GroupBy(s => s.MarketComparableId!.Value).ToDictionary(g => g.Key, g => g.Sum(s => s.WeightedScore!.Value));
        var dataPoints = scoreByComparable
            .Where(kv => kv.Value != 0m && calcByComparable.TryGetValue(kv.Key, out var c) && c.TotalAdjustedValue.HasValue)
            .Select(kv => (X: (double)kv.Value, Y: (double)calcByComparable[kv.Key].TotalAdjustedValue!.Value))
            .ToList();
        var scatter = BuildScatter(dataPoints, subjectByFactor, rsq);

        // Q6: Adjust Final Value.
        var finalRows = PricingGridCommon.FinalRows(
            await PricingGridCommon.LoadFinalValueAsync(connection, method.PricingMethodId), method.UnitType, "มูลค่าสุทธิ (การถดถอย)", perUnitSuffix: false, usableArea);

        return new WqsSection
        {
            GroupNumber       = method.GroupNumber,
            GroupName         = method.GroupName,
            Context           = method.Context,
            ComparableHeaders = headers,
            Chunks            = PricingGridCommon.Chunks(comparableIds.Count, ComparablesPerTable),
            MoneyUnit         = PricingGridCommon.MoneyUnit(method.UnitType),
            Factors           = factors,
            Totals            = TotalsRow(factorIds, scoreRows, comparableIds),
            PriceRows         = BuildPriceRows(comparableIds, calcByComparable, method.UnitType),
            Rsq               = rsq?.Rsq,
            Steyx             = rsq?.Steyx,
            Intercept         = rsq?.Intercept,
            Slope             = rsq?.Slope,
            Forecast          = rsq?.Forecast,
            LowestEstimate    = rsq?.LowestEstimate,
            HighestEstimate   = rsq?.HighestEstimate,
            FinalRows         = finalRows,
            Scatter           = scatter
        };
    }  // LoadOneAsync

    // ── Derived cells (not stored; each mirrors the screen's own formula) ──────────

    /// <summary>คะแนน (การคำนวณ) per factor — mirrors the screen's calcWeightedScore (domain/calculateWQS.ts): round2(weight × intensity).</summary>
    internal static string? WeightedIntensity(decimal? weight, decimal? intensity) =>
        weight is null && intensity is null ? null : PricingGridCommon.Money(PricingGridCommon.Round2((weight ?? 0m) * (intensity ?? 0m)));

    /// <summary>The รวม row — mirrors the screen's total row: raw column sums (stored WeightedScore for the ถ่วงน้ำหนัก columns).</summary>
    internal static WqsTotalsRow TotalsRow(IReadOnlyList<Guid> factorIds, IReadOnlyList<ScoreRowView> scoreRows, IReadOnlyList<Guid> comparableIds)
    {
        var inTable = scoreRows.Where(s => factorIds.Contains(s.FactorId)).ToList();
        var subject = inTable.Where(s => s.MarketComparableId is null).ToList();
        static decimal? Sum(IEnumerable<decimal?> v) { var l = v.Where(x => x.HasValue).ToList(); return l.Count == 0 ? null : l.Sum(x => x!.Value); }
        var perFactor = subject.Select(s => s.FactorWeight.HasValue || s.Intensity.HasValue
            ? PricingGridCommon.Round2((s.FactorWeight ?? 0m) * (s.Intensity ?? 0m)) : (decimal?)null);
        return new WqsTotalsRow
        {
            Weight             = PricingGridCommon.Whole(Sum(subject.Select(s => s.FactorWeight))),
            Intensity          = PricingGridCommon.Whole(Sum(subject.Select(s => s.Intensity))),
            WeightedIntensity  = PricingGridCommon.Whole(Sum(perFactor)),
            SubjectScore       = PricingGridCommon.Whole(Sum(subject.Select(s => s.Score))),
            SubjectWeighted    = PricingGridCommon.Money(Sum(subject.Select(s => s.WeightedScore))),
            ComparableScores   = comparableIds.Select(c => PricingGridCommon.Whole(Sum(inTable.Where(s => s.MarketComparableId == c).Select(s => s.Score)))).ToList(),
            ComparableWeighted = comparableIds.Select(c => PricingGridCommon.Money(Sum(inTable.Where(s => s.MarketComparableId == c).Select(s => s.WeightedScore)))).ToList(),
        };
    }

    // ── Scatter geometry (computed in C# so the template only emits literals) ────────

    private const int ChartW = 380;
    private const int ChartH = 230;
    private const int ChartPad = 38;

    /// <summary>
    /// Builds pixel-space scatter geometry from comparable (X=score, Y=price) points, the
    /// subject point (X = ΣWeightedScore of the NULL rows, Y = Forecast), and the regression
    /// line (Intercept + Slope·x). Returns null on insufficient/degenerate data.
    /// </summary>
    private static WqsScatter? BuildScatter(
        List<(double X, double Y)> comparables,
        Dictionary<Guid, ScoreRow> subjectByFactor,
        RsqRow? rsq)
    {
        if (comparables.Count < 2)
            return null;

        // Subject point (optional): total subject weighted score vs forecast price.
        double? subjX = subjectByFactor.Values
            .Where(s => s.WeightedScore.HasValue)
            .Select(s => (double?)s.WeightedScore!.Value)
            .Sum();
        if (subjX == 0d) subjX = null;
        double? subjY = rsq?.Forecast.HasValue == true ? (double)rsq.Forecast!.Value : null;
        bool hasSubject = subjX.HasValue && subjY.HasValue;

        // Data ranges (include subject + regression-line endpoints so they stay in the box).
        var xs = comparables.Select(p => p.X).ToList();
        var ys = comparables.Select(p => p.Y).ToList();
        if (hasSubject) { xs.Add(subjX!.Value); ys.Add(subjY!.Value); }

        double xMin = xs.Min(), xMax = xs.Max();
        double yMin = ys.Min(), yMax = ys.Max();

        // Regression line endpoints over the x-range.
        double? lineY1 = null, lineY2 = null;
        if (rsq?.Intercept.HasValue == true && rsq.Slope.HasValue)
        {
            double a = (double)rsq.Intercept!.Value, b = (double)rsq.Slope!.Value;
            lineY1 = a + b * xMin;
            lineY2 = a + b * xMax;
            yMin = Math.Min(yMin, Math.Min(lineY1.Value, lineY2.Value));
            yMax = Math.Max(yMax, Math.Max(lineY1.Value, lineY2.Value));
        }

        if (xMax - xMin < 1e-9 || yMax - yMin < 1e-9)
            return null; // degenerate — can't scale an axis

        double left = ChartPad, right = ChartW - ChartPad / 2.0;
        double top = ChartPad / 2.0, bottom = ChartH - ChartPad;

        double Mx(double x) => left + (x - xMin) / (xMax - xMin) * (right - left);
        double My(double y) => bottom - (y - yMin) / (yMax - yMin) * (bottom - top); // invert Y

        var points = comparables
            .Select(p => new WqsScatterPoint { Cx = Mx(p.X), Cy = My(p.Y), IsSubject = false })
            .ToList();
        if (hasSubject)
            points.Add(new WqsScatterPoint { Cx = Mx(subjX!.Value), Cy = My(subjY!.Value), IsSubject = true });

        return new WqsScatter
        {
            Width = ChartW,
            Height = ChartH,
            Pad = ChartPad,
            PlotLeft = left,
            PlotTop = top,
            PlotRight = right,
            PlotBottom = bottom,
            Points = points,
            HasLine = lineY1.HasValue && lineY2.HasValue,
            LineX1 = lineY1.HasValue ? Mx(xMin) : 0,
            LineY1 = lineY1.HasValue ? My(lineY1.Value) : 0,
            LineX2 = lineY2.HasValue ? Mx(xMax) : 0,
            LineY2 = lineY2.HasValue ? My(lineY2.Value) : 0,
            XMinLabel = xMin.ToString("#,##0.##"),
            XMaxLabel = xMax.ToString("#,##0.##"),
            YMinLabel = yMin.ToString("N0"),
            YMaxLabel = yMax.ToString("N0")
        };
    }

    // ── Price band ───────────────────────────────────────────────────────────────

    /// <summary>
    /// The screen's price band, all rows always: stored inputs per comparable, blank where the comparable has none
    /// (adjusted offer only with an offer price; years / period only with a sale price). The bold last row is
    /// PricingCalculations.TotalAdjustedValue. No row at all when no comparable carries a price.
    /// </summary>
    private static IReadOnlyList<WqsPriceRow> BuildPriceRows(IReadOnlyList<Guid> comparableIds, IReadOnlyDictionary<Guid, CalcRow> calc, string? unitType)
    {
        if (!comparableIds.Any(id => calc.TryGetValue(id, out var c) && (HasOffer(c) || HasSale(c))))
            return [];

        var bahtUnit = PricingGridCommon.MoneyUnit(unitType);
        WqsPriceRow Row(string label, Func<CalcRow, string?> cell, bool emphasis = false, string? unit = null) => new()
        {
            Label = label,
            Unit = unit,
            Values = comparableIds.Select(id => calc.TryGetValue(id, out var c) ? cell(c) : null).ToList(),
            Emphasis = emphasis
        };

        return
        [
            Row("ราคาเสนอขาย", c => HasOffer(c) ? PricingGridCommon.Money(c.OfferingPrice) : null, unit: bahtUnit),
            Row("การปรับแก้ราคาเสนอขาย (%)", c => HasOffer(c) ? PricingGridCommon.Money(c.AdjustOfferPricePct) : null, unit: "%"),
            Row("ราคาเสนอขายสุทธิหลังปรับ", c => HasOffer(c) ? PricingGridCommon.Money(c.AdjustOfferPriceAmt) : null, unit: bahtUnit),
            Row("ราคาซื้อขาย", c => HasSale(c) ? PricingGridCommon.Money(c.SellingPrice) : null, unit: bahtUnit),
            Row("จำนวนปีนับจากวันซื้อขาย", c => HasSale(c) ? c.BuySellYear?.ToString(System.Globalization.CultureInfo.InvariantCulture) : null, unit: "ปี"),
            Row("อัตราปรับตามเวลา (% ต่อปี)", c => HasSale(c) ? PricingGridCommon.Money(c.AdjustedPeriodPct) : null, unit: "%"),
            Row("การปรับตามเวลาสะสม (%)", c => HasSale(c) ? PricingGridCommon.Money(c.CumulativeAdjPeriod) : null, unit: "%"),
            Row("ราคาสุทธิหลังปรับ", c => PricingGridCommon.Money(c.TotalAdjustedValue), emphasis: true, unit: bahtUnit),
        ];
    }

    private static bool HasOffer(CalcRow c) => c.OfferingPrice is > 0m;
    private static bool HasSale(CalcRow c) => c.SellingPrice is > 0m;

    // ── Private flat DTOs for Dapper mapping ─────────────────────────────────────

    private sealed class FactorNameRow
    {
        public Guid FactorId { get; init; }
        public string? FactorName { get; init; }
    }

    internal interface ScoreRowView
    {
        Guid FactorId { get; }
        Guid? MarketComparableId { get; }
        decimal? FactorWeight { get; }
        decimal? Intensity { get; }
        decimal? Score { get; }
        decimal? WeightedScore { get; }
    }

    internal sealed class ScoreRow : ScoreRowView
    {
        public Guid FactorId { get; init; }

        // Nullable: null = subject/collateral row (MarketComparableId IS NULL in DB).
        public Guid? MarketComparableId { get; init; }

        public int DisplaySequence { get; init; }
        public decimal? FactorWeight { get; init; }
        public decimal? Intensity { get; init; }
        public decimal? Score { get; init; }
        public decimal? WeightedScore { get; init; }
    }

    private sealed class CalcRow
    {
        public Guid MarketComparableId { get; init; }
        public decimal? TotalAdjustedValue { get; init; }
        public decimal? SellingPrice { get; init; }
        public int? BuySellYear { get; init; }
        public decimal? AdjustedPeriodPct { get; init; }
        public decimal? CumulativeAdjPeriod { get; init; }
        public decimal? OfferingPrice { get; init; }
        public decimal? AdjustOfferPricePct { get; init; }
        public decimal? AdjustOfferPriceAmt { get; init; }
    }

    private sealed class RsqRow
    {
        public decimal? Rsq { get; init; }
        public decimal? Steyx { get; init; }
        public decimal? Intercept { get; init; }
        public decimal? Slope { get; init; }
        public decimal? Forecast { get; init; }
        public decimal? LowestEstimate { get; init; }
        public decimal? HighestEstimate { get; init; }
    }
}
