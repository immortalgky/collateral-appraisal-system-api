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

    /// <summary>Which approach / role this page belongs to and whether it is a reference (printed in the page banner).</summary>
    public PricingMethodContext? Context { get; init; }

    // ── Comparable columns ────────────────────────────────────────────────────────

    /// <summary>"ตลาด n" + kind · date · unit per comparable, ordered by PricingComparableLinks.DisplaySequence.</summary>
    public IReadOnlyList<MarketHeader> ComparableHeaders { get; init; } = [];

    /// <summary>The comparable columns split into tables that fit the page.</summary>
    public IReadOnlyList<TableChunk> Chunks { get; init; } = [];

    // ── Scoring band (ปัจจัยให้คะแนน) ─────────────────────────────────────────────

    /// <summary>One row per factor in PricingFactorScores, ordered by its DisplaySequence.</summary>
    public IReadOnlyList<WqsFactorRow> Factors { get; init; } = [];

    /// <summary>The รวม row.</summary>
    public WqsTotalsRow Totals { get; init; } = new();

    // ── Price band (ราคาเสนอขายและการปรับตามเวลา) ─────────────────────────────────

    /// <summary>Rows of the price band, last one is the bold มูลค่าที่ปรับแล้ว. Empty when no comparable carries a price.</summary>
    public IReadOnlyList<WqsPriceRow> PriceRows { get; init; } = [];

    // ── Regression statistics ─────────────────────────────────────────────────────
    // Source: appraisal.PricingRsqResults (PricingMethodId = WQS method).

    /// <summary>Coefficient of decision — PricingRsqResults.CoefficientOfDecision.</summary>
    public decimal? Rsq { get; init; }

    /// <summary>Standard error — PricingRsqResults.StandardError.</summary>
    public decimal? Steyx { get; init; }

    /// <summary>Intersection point — PricingRsqResults.IntersectionPoint.</summary>
    public decimal? Intercept { get; init; }

    /// <summary>Slope — PricingRsqResults.Slope.</summary>
    public decimal? Slope { get; init; }

    /// <summary>Final value (มูลค่าสุทธิ (การถดถอย)) — PricingRsqResults.RsqFinalValue.</summary>
    public decimal? Forecast { get; init; }

    /// <summary>Lowest estimate — PricingRsqResults.LowestEstimate.</summary>
    public decimal? LowestEstimate { get; init; }

    /// <summary>Highest estimate — PricingRsqResults.HighestEstimate.</summary>
    public decimal? HighestEstimate { get; init; }

    /// <summary>ปรับมูลค่าสุดท้าย (Adjust Final Value) — from PricingFinalValues. Empty when the method has none.</summary>
    public IReadOnlyList<PricingFinalRow> FinalRows { get; init; } = [];

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

/// <summary>One factor row of the WQS scoring band; every cell is pre-formatted, null prints "-".</summary>
public sealed class WqsFactorRow
{
    /// <summary>ปัจจัย — Thai translation of the factor name.</summary>
    public string? FactorName { get; init; }

    /// <summary>น้ำหนัก — PricingFactorScores.FactorWeight (collateral row).</summary>
    public string? Weight { get; init; }

    /// <summary>ความเข้ม — PricingFactorScores.Intensity (collateral row).</summary>
    public string? Intensity { get; init; }

    /// <summary>คะแนน (การคำนวณ) = น้ำหนัก × ความเข้ม, as the screen computes it.</summary>
    public string? WeightedIntensity { get; init; }

    /// <summary>ทรัพย์ คะแนน — PricingFactorScores.Score (collateral row).</summary>
    public string? SubjectScore { get; init; }

    /// <summary>ทรัพย์ ถ่วงน้ำหนัก — PricingFactorScores.WeightedScore (collateral row).</summary>
    public string? SubjectWeighted { get; init; }

    /// <summary>Per comparable คะแนน, in <see cref="WqsSection.ComparableHeaders"/> order.</summary>
    public IReadOnlyList<string?> ComparableScores { get; init; } = [];

    /// <summary>Per comparable ถ่วงน้ำหนัก (stored WeightedScore).</summary>
    public IReadOnlyList<string?> ComparableWeighted { get; init; } = [];
}

/// <summary>The รวม row of the WQS scoring band (sums of the table's own columns).</summary>
public sealed class WqsTotalsRow
{
    public string? Weight { get; init; }
    public string? Intensity { get; init; }
    public string? WeightedIntensity { get; init; }
    public string? SubjectScore { get; init; }
    public string? SubjectWeighted { get; init; }
    public IReadOnlyList<string?> ComparableScores { get; init; } = [];
    public IReadOnlyList<string?> ComparableWeighted { get; init; } = [];
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

/// <summary>One row of the WQS price band: a label and one pre-formatted value per comparable (blank when the comparable has none).</summary>
public sealed class WqsPriceRow
{
    public string? Label { get; init; }
    public IReadOnlyList<string?> Values { get; init; } = [];
    public bool Emphasis { get; init; }
}

/// <summary>One plotted point in <see cref="WqsScatter"/> (pixel coords).</summary>
public sealed class WqsScatterPoint
{
    public double Cx { get; init; }
    public double Cy { get; init; }

    /// <summary>True for the subject/forecast point (rendered as a distinct marker).</summary>
    public bool IsSubject { get; init; }
}
