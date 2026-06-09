namespace Reporting.Application.Models.Sections;

/// <summary>
/// Sub-model for the "ตารางการปรับมูลค่าหลักทรัพย์ (วิธีปรับเปรียบเทียบข้อมูลตลาด)"
/// (Sale Grid / Direct Comparison) section — FSD §2.1.2.10.
///
/// The table is column-per-comparable, so data is stored pre-transposed:
/// <c>ComparableHeaders</c> drives the column headings and <c>Rows</c> each provide
/// one value per comparable column in the same order.
///
/// All scalar properties are nullable so that partial data renders as blank.
/// The section is absent from the report when <see cref="SaleGridSectionLoader.LoadAsync"/>
/// returns <see langword="null"/> (appraisal has no SaleGrid or DirectComparison method).
/// </summary>
public sealed class SaleGridSection
{
    // ── Column headers ────────────────────────────────────────────────────────

    /// <summary>
    /// One header label per comparable column, e.g. ["ข้อมูล 1", "ข้อมูล 2", ...].
    /// Ordered by <c>PricingComparableLinks.DisplaySequence</c>.
    /// Source: appraisal.PricingComparableLinks (DisplaySequence → ordinal label).
    /// </summary>
    public IReadOnlyList<string> ComparableHeaders { get; init; } = [];

    // ── Grid rows (transposed: one row per adjustment line) ───────────────────

    /// <summary>
    /// Adjustment rows. Each row has a Thai label and one value string per comparable.
    /// The order matches the FSD §2.1.2.10 image layout:
    /// <list type="bullet">
    ///   <item>ราคาเสนอขาย (OfferingPrice)</item>
    ///   <item>ปีที่ซื้อขาย / ราคาที่ซื้อขาย (BuySellYear / SellingPrice)</item>
    ///   <item>ปรับค่าตามช่วงเวลา % (AdjustedPeriodPct)</item>
    ///   <item>ส่วนขาดที่ดิน / ราคา / ค่าปรับ (LandAreaDeficient / LandPrice / LandValueAdjustment)</item>
    ///   <item>ส่วนขาดอาคาร / ราคา / ค่าปรับ (UsableAreaDeficient / UsableAreaPrice / BuildingValueAdjustment)</item>
    ///   <item>ราคาหลังปรับค่า (TotalAdjustedValue — before factor adj)</item>
    ///   <item>ปรับปัจจัย % / จำนวนเงิน (TotalFactorDiffPct / TotalFactorDiffAmt)</item>
    ///   <item>รวมค่าปรับทั้งหมด (TotalAdjustedValue)</item>
    ///   <item>มูลค่าทรัพย์สิน (WeightedAdjustedValue or TotalAdjustedValue)</item>
    ///   <item>Dynamic factor rows from PricingFactorScores (one row per factor)</item>
    /// </list>
    /// </summary>
    public IReadOnlyList<SaleGridRow> Rows { get; init; } = [];

    // ── Summary footer ────────────────────────────────────────────────────────

    /// <summary>
    /// สรุปมูลค่าทรัพย์สิน — source: appraisal.PricingAnalysisMethods.MethodValue.
    /// Null when MethodValue has not been set.
    /// </summary>
    public decimal? SummaryValue { get; init; }

    /// <summary>
    /// สรุปมูลค่าทรัพย์สิน (ปัดเศษ) — MethodValue rounded to nearest 1 000.
    /// Derived server-side; no dedicated DB column.
    /// </summary>
    public decimal? SummaryValueRounded { get; init; }
}

/// <summary>
/// One horizontal row in the Sale Grid adjustment table.
///
/// <c>Label</c> is the Thai row heading (first column).
/// <c>Values</c> contains one formatted string per comparable in
/// <c>ComparableHeaders</c> order; null entries render as blank.
/// </summary>
public sealed class SaleGridRow
{
    /// <summary>Thai row label (first column), e.g. "ราคาเสนอขาย".</summary>
    public string? Label { get; init; }

    /// <summary>
    /// One value per comparable column, in the same order as
    /// <see cref="SaleGridSection.ComparableHeaders"/>.
    /// A null element means the comparable has no data for this row.
    /// </summary>
    public IReadOnlyList<string?> Values { get; init; } = [];
}
