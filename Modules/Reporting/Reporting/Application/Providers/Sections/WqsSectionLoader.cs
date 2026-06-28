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
///   Q2  appraisal.PricingComparableLinks (PricingMethodId, DisplaySequence)
///       → comparable header labels ("ข้อมูล {seq}"), ordered by DisplaySequence.
///
///   Q3  appraisal.PricingComparativeFactors (PricingMethodId, FactorId, DisplaySequence)
///       JOIN appraisal.MarketComparableFactors + appraisal.MarketComparableFactorTranslations
///       → factor display names (Thai preferred, English fallback), ordered by DisplaySequence.
///       WHERE IsSelectedForScoring = 1 (only scored factors appear in the WQS table).
///
///   Q4  appraisal.PricingFactorScores (PricingMethodId, MarketComparableId, FactorId,
///       FactorWeight, Intensity, Score, DisplaySequence)
///       → subject row  (MarketComparableId IS NULL): Weight, Intensity, Score per factor.
///       → comparable rows (MarketComparableId IS NOT NULL): Score per factor per comparable.
///
///   Q5  appraisal.PricingRsqResults (PricingMethodId)
///       → regression statistics (Rsq, Steyx, Intercept, Slope, Forecast,
///         LowestEstimate, HighestEstimate).
///
/// Column sources confirmed against PricingConfiguration.cs and
/// MarketComparableFactorConfiguration.cs. No HasColumnName overrides present on
/// any of the five tables — DB column names match C# property names exactly.
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
    public static async Task<IReadOnlyList<WqsSection>> LoadAllAsync(
        IDbConnection connection,
        Guid appraisalId,
        CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("AppraisalId", appraisalId);

        // ── Q1: Resolve all WQS PricingMethodIds (one per property group) ─────────
        // Navigation path:
        //   appraisal.PropertyGroups → AnchorId in appraisal.PricingAnalysis
        //     (SubjectType = 0 = PropertyGroup)
        //   → appraisal.PricingAnalysisApproaches (PricingAnalysisId)
        //   → appraisal.PricingAnalysisMethods    (ApproachId, MethodType = 'WQS')
        //
        // pg.GroupNumber and pg.GroupName are included so the template can print a
        // กลุ่มที่ N bar when the list size > 1 (mirrors appraisal-book.html:30-31).
        //
        // ORDER BY pg.GroupNumber, pam.Id — groups appear in group order; within a
        // group, if multiple methods exist, the first by pam.Id is used (taken in C#).
        //
        // MethodValue  → AppraisalValue  (PricingAnalysisMethodConfiguration: decimal 18,2)
        // ValuePerUnit → LandPerSqWa / PricePerUnit (same column; decimal 18,2)
        const string methodSql = """
            SELECT
                pg.Id          AS PropertyGroupId,
                pg.GroupNumber,
                pg.GroupName,
                pam.Id         AS PricingMethodId,
                pam.MethodValue,
                pam.ValuePerUnit
            FROM appraisal.PropertyGroups pg
            JOIN appraisal.PricingAnalysis pa
                ON pa.AnchorId  = pg.Id
               AND pa.SubjectType = 0
            JOIN appraisal.PricingAnalysisApproaches paa
                ON paa.PricingAnalysisId = pa.Id
            JOIN appraisal.PricingAnalysisMethods pam
                ON pam.ApproachId = paa.Id
               AND pam.MethodType = 'WQS'
            WHERE pg.AppraisalId = @AppraisalId
            ORDER BY pg.GroupNumber, pam.Id
            """;

        var allMethodRows = (await connection.QueryAsync<MethodRow>(methodSql, p)).ToList();
        if (allMethodRows.Count == 0)
            return [];

        // Take the first PricingMethodId per PropertyGroup (lowest pam.Id within group).
        var methodsPerGroup = allMethodRows
            .GroupBy(r => r.PropertyGroupId)
            .Select(g => g.First())        // already ordered by pam.Id within each group
            .OrderBy(r => r.GroupNumber)   // final list ordered by group number
            .ToList();

        var sections = new List<WqsSection>(methodsPerGroup.Count);
        foreach (var method in methodsPerGroup)
        {
            var section = await LoadOneAsync(connection, method, ct);
            if (section is not null)
                sections.Add(section);
        }

        return sections;
    }

    // ── Per-method loader (unchanged logic extracted from the original LoadAsync) ──

    private static async Task<WqsSection?> LoadOneAsync(
        IDbConnection connection,
        MethodRow method,
        CancellationToken ct)
    {
        var mp = new DynamicParameters();
        mp.Add("PricingMethodId", method.PricingMethodId);

        // ── Q2: Comparable headers ────────────────────────────────────────────────
        // Source: appraisal.PricingComparableLinks
        //   PricingMethodId (required, FK to PricingAnalysisMethods)
        //   MarketComparableId (required)
        //   DisplaySequence (int, required)
        // Header label = "ข้อมูล {DisplaySequence}".
        const string comparablesSql = """
            SELECT DisplaySequence
            FROM appraisal.PricingComparableLinks
            WHERE PricingMethodId = @PricingMethodId
            ORDER BY DisplaySequence
            """;

        var comparableSeqs = (await connection.QueryAsync<int>(comparablesSql, mp)).ToList();
        var comparableHeaders = comparableSeqs
            .Select(seq => $"ข้อมูล {seq}")
            .ToList();

        // Also need the ordered MarketComparableId list to pivot scores later (Q4).
        const string comparableIdsSql = """
            SELECT MarketComparableId
            FROM appraisal.PricingComparableLinks
            WHERE PricingMethodId = @PricingMethodId
            ORDER BY DisplaySequence
            """;

        var comparableIds = (await connection.QueryAsync<Guid>(comparableIdsSql, mp)).ToList();

        // ── Q3: Factor names (Thai preferred) ────────────────────────────────────
        // Source tables confirmed against MarketComparableFactorConfiguration.cs:
        //   appraisal.MarketComparableFactors          (Id, FactorCode, FieldName, IsActive)
        //   appraisal.MarketComparableFactorTranslations (MarketComparableFactorId, Language,
        //                                                 FactorName)
        //
        // PricingComparativeFactors columns confirmed against
        // PricingComparativeFactorConfiguration.cs:
        //   PricingMethodId, FactorId, DisplaySequence, IsSelectedForScoring (bit default 0)
        //
        // We LEFT JOIN translations twice: prefer 'th', fall back to 'en'.
        // Only factors with IsSelectedForScoring = 1 appear in the WQS scoring table.
        const string factorsSql = """
            SELECT
                pcf.FactorId,
                pcf.DisplaySequence,
                COALESCE(th.FactorName, en.FactorName, mcf.FieldName) AS FactorName
            FROM appraisal.PricingComparativeFactors pcf
            JOIN appraisal.MarketComparableFactors mcf
                ON mcf.Id = pcf.FactorId
            LEFT JOIN appraisal.MarketComparableFactorTranslations th
                ON th.MarketComparableFactorId = mcf.Id
               AND th.Language = 'th'
            LEFT JOIN appraisal.MarketComparableFactorTranslations en
                ON en.MarketComparableFactorId = mcf.Id
               AND en.Language = 'en'
            WHERE pcf.PricingMethodId = @PricingMethodId
              AND pcf.IsSelectedForScoring = 1
            ORDER BY pcf.DisplaySequence
            """;

        var factorRows = (await connection.QueryAsync<FactorNameRow>(factorsSql, mp)).ToList();
        if (factorRows.Count == 0)
        {
            // WQS method exists but no factors selected for scoring.
            // Still return a section (not null) so the template can render headings.
        }

        // ── Q4: Factor scores (subject + comparables) ─────────────────────────────
        // Source: appraisal.PricingFactorScores
        //   PricingMethodId, MarketComparableId (nullable; NULL = subject/collateral),
        //   FactorId, FactorWeight (decimal 5,2), Intensity (decimal 5,2),
        //   Score (decimal 5,2), DisplaySequence.
        // Unique index: (PricingMethodId, MarketComparableId, FactorId).
        const string scoresSql = """
            SELECT
                FactorId,
                MarketComparableId,
                FactorWeight,
                Intensity,
                Score,
                WeightedScore
            FROM appraisal.PricingFactorScores
            WHERE PricingMethodId = @PricingMethodId
            """;

        var scoreRows = (await connection.QueryAsync<ScoreRow>(scoresSql, mp)).ToList();

        // The subject/collateral row has MarketComparableId = NULL and carries the factor's
        // weight/intensity/score. A null Guid? CANNOT be a Dictionary key (Dictionary.Add
        // rejects null keys regardless of comparer), so split it out instead of keying on null:
        //   subjectByFactor:     factorId → subject ScoreRow (weight/intensity/score)
        //   compScoresByFactor:  factorId → (comparableId → ScoreRow)  [non-null ids only]
        var subjectByFactor = scoreRows
            .Where(s => s.MarketComparableId is null)
            .GroupBy(s => s.FactorId)
            .ToDictionary(g => g.Key, g => g.First());

        var compScoresByFactor = scoreRows
            .Where(s => s.MarketComparableId is not null)
            .GroupBy(s => s.FactorId)
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(s => s.MarketComparableId!.Value, s => s));

        // ── Q5: Regression statistics ─────────────────────────────────────────────
        // Source: appraisal.PricingRsqResults
        //   PricingMethodId (unique index), CoefficientOfDecision (decimal 18,10) → Rsq,
        //   StandardError (decimal 18,2) → Steyx,
        //   IntersectionPoint (decimal 18,2) → Intercept,
        //   Slope (decimal 18,2), RsqFinalValue (decimal 18,2) → Forecast,
        //   LowestEstimate (decimal 18,2), HighestEstimate (decimal 18,2).
        const string rsqSql = """
            SELECT
                CoefficientOfDecision AS Rsq,
                StandardError         AS Steyx,
                IntersectionPoint     AS Intercept,
                Slope,
                RsqFinalValue         AS Forecast,
                LowestEstimate,
                HighestEstimate
            FROM appraisal.PricingRsqResults
            WHERE PricingMethodId = @PricingMethodId
            """;

        var rsq = await connection.QueryFirstOrDefaultAsync<RsqRow>(rsqSql, mp);

        // ── Build WqsFactorRow list ───────────────────────────────────────────────
        var factors = factorRows.Select(f =>
        {
            subjectByFactor.TryGetValue(f.FactorId, out var subjectScore);
            compScoresByFactor.TryGetValue(f.FactorId, out var byComparable);

            // Pivot comparable scores in the same order as comparableIds
            var comparableScores = comparableIds
                .Select(cid =>
                {
                    if (byComparable is not null && byComparable.TryGetValue(cid, out var cs))
                        return cs.Score;
                    return (decimal?)null;
                })
                .ToList();

            return new WqsFactorRow
            {
                FactorName       = f.FactorName,
                Weight           = subjectScore?.FactorWeight,
                Intensity        = subjectScore?.Intensity,
                Score            = subjectScore?.Score,
                ComparableScores = comparableScores
            };
        }).ToList();

        // ── Q6: Per-comparable calculation row (scatter + price block) ────────────
        // Scatter: X = total weighted score per comparable; Y = TotalAdjustedValue.
        // Price block (FSD §2.1.2.9): purchase/offer prices + their adjustments, sourced
        // from PricingCalculations (populated for WQS too — see SaveComparativeAnalysis).
        //   ราคาซื้อขาย            = SellingPrice
        //   การปรับแก้ราคาซื้อขาย % = CumulativeAdjPeriod
        //   ราคาซื้อขายสุทธิหลังปรับ = SellingPrice × (1 + CumulativeAdjPeriod/100)
        //   ราคาเสนอขาย            = OfferingPrice
        //   การปรับแก้ราคาเสนอขาย % = AdjustOfferPricePct
        //   ราคาเสนอขายสุทธิหลังปรับ = AdjustOfferPriceAmt (override) | OfferingPrice × (1 − AdjustOfferPricePct/100)
        // "after adjust" math mirrors PricingCalculationHelper.ComputeInitialPrice.
        const string calcSql = """
            SELECT
                MarketComparableId,
                TotalAdjustedValue,
                SellingPrice,
                CumulativeAdjPeriod,
                OfferingPrice,
                AdjustOfferPricePct,
                AdjustOfferPriceAmt
            FROM appraisal.PricingCalculations
            WHERE PricingMethodId = @PricingMethodId
            """;
        var calcByComparable = (await connection.QueryAsync<CalcRow>(calcSql, mp))
            .GroupBy(c => c.MarketComparableId)
            .ToDictionary(g => g.Key, g => g.First());

        var priceByComparable = calcByComparable
            .Where(kv => kv.Value.TotalAdjustedValue.HasValue)
            .ToDictionary(kv => kv.Key, kv => kv.Value.TotalAdjustedValue!.Value);

        // Total weighted score per comparable (skip null weighted scores).
        var scoreByComparable = scoreRows
            .Where(s => s.MarketComparableId is not null && s.WeightedScore.HasValue)
            .GroupBy(s => s.MarketComparableId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.WeightedScore!.Value));

        // Comparable (X, Y) pairs — both must be present (WqsCalculationService rule).
        var dataPoints = scoreByComparable
            .Where(kv => kv.Value != 0m && priceByComparable.ContainsKey(kv.Key))
            .Select(kv => (X: (double)kv.Value, Y: (double)priceByComparable[kv.Key]))
            .ToList();

        var scatter = BuildScatter(dataPoints, subjectByFactor, rsq);

        // ── Totals row (ผลรวม) ────────────────────────────────────────────────────
        // ΣWeight and Σ(Weight×Intensity) across factors; per-comparable and subject
        // totals = Σ(Score×Weight) = ΣWeightedScore (the scatter X axis).
        decimal? totalWeight = factors.Any(f => f.Weight.HasValue)
            ? factors.Sum(f => f.Weight ?? 0m)
            : null;
        decimal? totalMaxScore = factors.Any(f => f.Weight.HasValue && f.Intensity.HasValue)
            ? factors.Sum(f => (f.Weight ?? 0m) * (f.Intensity ?? 0m))
            : null;

        var comparableScoreTotals = comparableIds
            .Select(cid => scoreByComparable.TryGetValue(cid, out var t) ? (decimal?)t : null)
            .ToList();

        decimal? subjectScoreTotal = subjectByFactor.Values.Any(s => s.WeightedScore.HasValue)
            ? subjectByFactor.Values.Sum(s => s.WeightedScore ?? 0m)
            : null;

        // ── Price block (FSD §2.1.2.9) ────────────────────────────────────────────
        var priceRows = BuildPriceRows(comparableIds, calcByComparable);

        // ── เนื้อที่ดิน = ราคาประเมิน ÷ ตารางวาละ ──────────────────────────────────
        decimal? landArea =
            method.MethodValue.HasValue && method.ValuePerUnit.HasValue && method.ValuePerUnit.Value != 0m
                ? method.MethodValue.Value / method.ValuePerUnit.Value
                : null;

        // ── Build section ─────────────────────────────────────────────────────────
        return new WqsSection
        {
            GroupNumber           = method.GroupNumber,
            GroupName             = method.GroupName,
            ComparableHeaders     = comparableHeaders,
            Factors               = factors,
            TotalWeight           = totalWeight,
            TotalMaxScore         = totalMaxScore,
            ComparableScoreTotals = comparableScoreTotals,
            SubjectScoreTotal     = subjectScoreTotal,
            PriceRows             = priceRows,
            LandArea              = landArea,
            Rsq                   = rsq?.Rsq,
            Steyx             = rsq?.Steyx,
            Intercept         = rsq?.Intercept,
            Slope             = rsq?.Slope,
            Forecast          = rsq?.Forecast,
            LowestEstimate    = rsq?.LowestEstimate,
            HighestEstimate   = rsq?.HighestEstimate,
            LandPerSqWa       = method.ValuePerUnit,
            PricePerUnit      = method.ValuePerUnit,
            AppraisalValue    = method.MethodValue,
            Scatter           = scatter
        };
    }  // LoadOneAsync

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
            XMinLabel = xMin.ToString("0.##"),
            XMaxLabel = xMax.ToString("0.##"),
            YMinLabel = yMin.ToString("N0"),
            YMaxLabel = yMax.ToString("N0")
        };
    }

    // ── Price block construction (FSD §2.1.2.9) ──────────────────────────────────

    /// <summary>
    /// Builds the six price-adjustment rows from per-comparable <see cref="CalcRow"/>s.
    /// A row is emitted only when at least one comparable carries a value, so a WQS
    /// analysis scored purely on quality factors shows no empty price block.
    /// </summary>
    private static IReadOnlyList<WqsPriceRow> BuildPriceRows(
        IReadOnlyList<Guid> comparableIds,
        IReadOnlyDictionary<Guid, CalcRow> calcByComparable)
    {
        const string Unit = "บาท/ตารางวา";

        WqsPriceRow? Row(string label, Func<CalcRow, decimal?> selector)
        {
            var values = comparableIds
                .Select(id => calcByComparable.TryGetValue(id, out var c) ? selector(c) : null)
                .Select(v => v.HasValue ? v.Value.ToString("N2") : null)
                .ToList();
            return values.Any(v => v is not null)
                ? new WqsPriceRow { Label = label, Unit = Unit, Values = values }
                : null;
        }

        // ราคาซื้อขายสุทธิหลังปรับ = SellingPrice × (1 + CumulativeAdjPeriod/100)
        static decimal? PurchaseNet(CalcRow c) =>
            c.SellingPrice.HasValue
                ? c.SellingPrice.Value * (1m + (c.CumulativeAdjPeriod ?? 0m) / 100m)
                : null;

        // ราคาเสนอขายสุทธิหลังปรับ = AdjustOfferPriceAmt (override) | OfferingPrice × (1 − pct/100)
        static decimal? OfferNet(CalcRow c)
        {
            if (c.AdjustOfferPriceAmt is { } amt && amt != 0m) return amt;
            if (c.OfferingPrice.HasValue)
                return c.OfferingPrice.Value * (1m - (c.AdjustOfferPricePct ?? 0m) / 100m);
            return null;
        }

        var rows = new List<WqsPriceRow?>
        {
            Row("ราคาซื้อขาย", c => c.SellingPrice),
            Row("การปรับแก้ราคาซื้อขาย (%)", c => c.CumulativeAdjPeriod),
            Row("ราคาซื้อขายสุทธิหลังปรับ", PurchaseNet),
            Row("ราคาเสนอขาย", c => c.OfferingPrice),
            Row("การปรับแก้ราคาเสนอขาย (%)", c => c.AdjustOfferPricePct),
            Row("ราคาเสนอขายสุทธิหลังปรับ", OfferNet),
        };

        return rows.Where(r => r is not null).Select(r => r!).ToList();
    }

    // ── Private flat DTOs for Dapper mapping ─────────────────────────────────────

    private sealed class MethodRow
    {
        public Guid    PropertyGroupId  { get; init; }
        public int     GroupNumber      { get; init; }
        public string? GroupName        { get; init; }
        public Guid    PricingMethodId  { get; init; }
        public decimal? MethodValue     { get; init; }
        public decimal? ValuePerUnit    { get; init; }
    }

    private sealed class FactorNameRow
    {
        public Guid FactorId { get; init; }
        public int DisplaySequence { get; init; }
        public string? FactorName { get; init; }
    }

    private sealed class ScoreRow
    {
        public Guid FactorId { get; init; }

        // Nullable: null = subject/collateral row (MarketComparableId IS NULL in DB).
        public Guid? MarketComparableId { get; init; }

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
