namespace Reporting.Application.Models.Sections;

/// <summary>
/// Sub-model for the "WQS — Weighted Quality Score" comparison section — FSD §2.1.2.9.
///
/// All scalar properties are nullable so that partial data renders as blank.
/// The section is absent from the report when <see cref="WqsSectionLoader.LoadAllAsync"/>
/// returns an empty list (appraisal has no WQS method).
/// </summary>
public sealed class WqsSection
{
    // ── Property group routing ────────────────────────────────────────────────────

    /// <summary>
    /// PropertyGroups.GroupNumber for the group this WQS method belongs to.
    /// 0 when ungrouped. Used to print a กลุ่มที่ N bar when multiple groups
    /// use WQS (mirrors the land/building group bar in appraisal-book.html:30-31).
    /// </summary>
    public int GroupNumber { get; init; }

    /// <summary>PropertyGroups.GroupName for the group, or null when no name is set.</summary>
    public string? GroupName { get; init; }

    // ── Comparable column headers ─────────────────────────────────────────────────

    /// <summary>
    /// Headers for the comparable columns, e.g. "ข้อมูล 1", "ข้อมูล 2", …
    /// Ordered by PricingComparableLinks.DisplaySequence.
    /// Empty when no comparables are linked.
    /// </summary>
    public IReadOnlyList<string> ComparableHeaders { get; init; } = [];

    // ── Factor rows ───────────────────────────────────────────────────────────────

    /// <summary>
    /// One row per scoring factor, ordered by PricingComparativeFactors.DisplaySequence.
    /// </summary>
    public IReadOnlyList<WqsFactorRow> Factors { get; init; } = [];

    // ── Totals row (ผลรวม) — FSD §2.1.2.9 ─────────────────────────────────────────

    /// <summary>ผลรวม ค่าน้ำหนัก — ΣWeight across all factors.</summary>
    public decimal? TotalWeight { get; init; }

    /// <summary>
    /// ผลรวม คะแนน — Σ(Weight × Intensity) across all factors. This is the maximum
    /// attainable weighted score (the คะแนน column total).
    /// </summary>
    public decimal? TotalMaxScore { get; init; }

    /// <summary>
    /// Per-comparable total weighted score (Σ Score×Weight), in the same order as
    /// <see cref="ComparableHeaders"/>. Also the คะแนนเฉลี่ย row and the scatter X axis.
    /// Null element when a comparable has no scored factors.
    /// </summary>
    public IReadOnlyList<decimal?> ComparableScoreTotals { get; init; } = [];

    /// <summary>ทรัพย์สิน (SP) total weighted score — Σ subject Score×Weight.</summary>
    public decimal? SubjectScoreTotal { get; init; }

    // ── Price block (ราคาซื้อขาย / ราคาเสนอขาย) — FSD §2.1.2.9 ─────────────────────

    /// <summary>
    /// Price-adjustment rows (purchase price, its %, net; offer price, its %, net),
    /// one value per comparable column. Empty when no comparable carries price data
    /// (e.g. a WQS analysis scored purely on quality factors).
    /// </summary>
    public IReadOnlyList<WqsPriceRow> PriceRows { get; init; } = [];

    // ── Regression statistics ─────────────────────────────────────────────────────
    // Source: appraisal.PricingRsqResults (PricingMethodId = WQS method).

    /// <summary>สัมประสิทธิ์การกำหนด (R²) — source: PricingRsqResults.CoefficientOfDecision.</summary>
    public decimal? Rsq { get; init; }

    /// <summary>ค่าผิดพลาดมาตรฐาน (STEYX) — source: PricingRsqResults.StandardError.</summary>
    public decimal? Steyx { get; init; }

    /// <summary>จุดตัดแกน (INTERCEPT) — source: PricingRsqResults.IntersectionPoint.</summary>
    public decimal? Intercept { get; init; }

    /// <summary>ความชัน (SLOPE) — source: PricingRsqResults.Slope.</summary>
    public decimal? Slope { get; init; }

    /// <summary>มูลค่าประมาณการ (FORECAST) — source: PricingRsqResults.RsqFinalValue.</summary>
    public decimal? Forecast { get; init; }

    /// <summary>ประมาณการต่ำสุด — source: PricingRsqResults.LowestEstimate.</summary>
    public decimal? LowestEstimate { get; init; }

    /// <summary>ประมาณการสูงสุด — source: PricingRsqResults.HighestEstimate.</summary>
    public decimal? HighestEstimate { get; init; }

    // ── Summary values ────────────────────────────────────────────────────────────

    /// <summary>
    /// ราคาต่อ ตร.วา — source: PricingAnalysisMethods.ValuePerUnit.
    /// This is the per-square-wa unit price stored directly on the method row.
    /// (LandPerSqWa = ValuePerUnit; Forecast from RsqResults is the regression
    /// predicted value and is a separate figure. We use ValuePerUnit here because
    /// it is the authoritative user-confirmed per-unit price on the method.)
    /// </summary>
    public decimal? LandPerSqWa { get; init; }

    /// <summary>
    /// ราคาประเมิน — source: PricingAnalysisMethods.MethodValue.
    /// The final appraised value for this WQS method.
    /// </summary>
    public decimal? AppraisalValue { get; init; }

    /// <summary>
    /// เนื้อที่ดิน (ตารางวา) — derived land area = MethodValue / ValuePerUnit
    /// (ราคาประเมิน ÷ ตารางวาละ). Null when either input is missing or ValuePerUnit is 0.
    /// </summary>
    public decimal? LandArea { get; init; }

    /// <summary>
    /// ราคาต่อหน่วย — source: PricingAnalysisMethods.ValuePerUnit (same column as LandPerSqWa).
    /// Exposed separately so templates may render either label without aliasing in code.
    /// </summary>
    public decimal? PricePerUnit { get; init; }

    // ── Scatter graph (FSD §2.1.2.9, image17) ─────────────────────────────────────

    /// <summary>
    /// Pre-computed pixel-space geometry for the regression scatter graph, or
    /// <see langword="null"/> when there is insufficient/degenerate data
    /// (&lt; 2 comparable points, or a zero-width/height data range).
    /// All coordinates are already mapped to SVG space by the loader so the template
    /// only emits literals.
    /// </summary>
    public WqsScatter? Scatter { get; init; }
}

/// <summary>
/// One factor row in the WQS scoring table.
/// </summary>
public sealed class WqsFactorRow
{
    /// <summary>
    /// ปัจจัย — Thai factor name resolved from appraisal.MarketComparableFactorTranslations
    /// (Language='th'). Falls back to the 'en' translation when no Thai translation exists.
    /// </summary>
    public string? FactorName { get; init; }

    /// <summary>
    /// ค่าน้ำหนัก — source: PricingFactorScores.FactorWeight (subject row, MarketComparableId IS NULL).
    /// </summary>
    public decimal? Weight { get; init; }

    /// <summary>
    /// ความเข้ม — source: PricingFactorScores.Intensity (subject row, MarketComparableId IS NULL).
    /// </summary>
    public decimal? Intensity { get; init; }

    /// <summary>
    /// คะแนน — source: PricingFactorScores.Score (subject row, MarketComparableId IS NULL).
    /// This is the subject's own quality score for this factor.
    /// </summary>
    public decimal? Score { get; init; }

    /// <summary>
    /// Per-comparable scores, in the same order as <see cref="WqsSection.ComparableHeaders"/>.
    /// Source: PricingFactorScores.Score per linked MarketComparableId, ordered by
    /// PricingComparableLinks.DisplaySequence. Null element when a score row is absent.
    /// </summary>
    public IReadOnlyList<decimal?> ComparableScores { get; init; } = [];
}

/// <summary>
/// Pixel-space geometry for the WQS regression scatter graph (rendered as inline SVG).
/// X axis = total weighted quality score, Y axis = price per unit. The loader computes
/// all coordinates so the template is purely declarative.
/// </summary>
public sealed class WqsScatter
{
    /// <summary>SVG viewBox width (px).</summary>
    public int Width { get; init; }

    /// <summary>SVG viewBox height (px).</summary>
    public int Height { get; init; }

    /// <summary>Plot-area inset from each edge (px) — leaves room for axes/labels.</summary>
    public int Pad { get; init; }

    /// <summary>Plot-box edges in pixel coords (axes drawn on Left + Bottom).</summary>
    public double PlotLeft { get; init; }
    public double PlotTop { get; init; }
    public double PlotRight { get; init; }
    public double PlotBottom { get; init; }

    /// <summary>Scatter points (comparables + the subject/forecast point), pixel coords.</summary>
    public IReadOnlyList<WqsScatterPoint> Points { get; init; } = [];

    /// <summary>True when a regression line should be drawn (Intercept+Slope present).</summary>
    public bool HasLine { get; init; }

    /// <summary>Regression line endpoints (pixel coords, clamped to the plot box).</summary>
    public double LineX1 { get; init; }
    public double LineY1 { get; init; }
    public double LineX2 { get; init; }
    public double LineY2 { get; init; }

    /// <summary>Axis tick labels (display strings).</summary>
    public string? XMinLabel { get; init; }
    public string? XMaxLabel { get; init; }
    public string? YMinLabel { get; init; }
    public string? YMaxLabel { get; init; }

    /// <summary>Axis titles.</summary>
    public string XAxisTitle { get; init; } = "คะแนนคุณภาพ";
    public string YAxisTitle { get; init; } = "ราคา/หน่วย";
}

/// <summary>
/// One price-block row in the WQS table (e.g. ราคาซื้อขาย / ราคาเสนอขายสุทธิหลังปรับ).
/// Spans the same comparable columns as the factor rows; the subject (SP) column is blank.
/// </summary>
public sealed class WqsPriceRow
{
    /// <summary>Thai row label (first column), e.g. "ราคาซื้อขาย".</summary>
    public string? Label { get; init; }

    /// <summary>Unit label shown in the คะแนน column, e.g. "บาท/ตารางวา".</summary>
    public string? Unit { get; init; }

    /// <summary>
    /// One pre-formatted value per comparable column, in <see cref="WqsSection.ComparableHeaders"/>
    /// order. A null element renders as blank.
    /// </summary>
    public IReadOnlyList<string?> Values { get; init; } = [];
}

/// <summary>One plotted point in <see cref="WqsScatter"/> (pixel coords).</summary>
public sealed class WqsScatterPoint
{
    public double Cx { get; init; }
    public double Cy { get; init; }

    /// <summary>True for the subject/forecast point (rendered as a distinct marker).</summary>
    public bool IsSubject { get; init; }
}
