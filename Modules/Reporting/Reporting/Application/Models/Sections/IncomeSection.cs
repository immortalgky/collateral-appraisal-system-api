namespace Reporting.Application.Models.Sections;

/// <summary>
/// Sub-model for the "วิธีรายได้ (DCF)" Income / Discounted Cash Flow pricing section.
///
/// Source: appraisal.IncomeAnalyses (FK: PricingAnalysisMethodId).
/// JSON summary arrays are deserialized and zipped by year index into <see cref="YearRows"/>.
/// Confirmed against IncomeAnalysisConfiguration.cs:
///   Table = "IncomeAnalyses" (schema appraisal)
///   FK column: PricingAnalysisMethodId (unique index)
///   Scalar columns: TotalNumberOfYears (int), CapitalizeRate (5,2), DiscountedRate (5,2),
///     FinalValueRounded (18,2), IsHighestBestUsed (bit), AppraisalPriceRounded (18,2)
///   Owned HighestBestUsed columns (HasColumnName verified):
///     HighestBestUsed_AreaRai, HighestBestUsed_AreaNgan, HighestBestUsed_AreaWa (18,2),
///     HighestBestUsed_PricePerSqWa (18,2)
///   Owned IncomeSummary JSON columns (HasColumnName verified):
///     Summary_GrossRevenueJson, Summary_TerminalRevenueJson, Summary_TotalNetJson,
///     Summary_DiscountJson, Summary_PresentValueJson  (nvarchar(max))
/// </summary>
public sealed class IncomeSection
{
    // ── Property group routing ────────────────────────────────────────────────────

    /// <summary>PropertyGroups.GroupNumber for the group this Income method belongs to.</summary>
    public int GroupNumber { get; init; }

    /// <summary>PropertyGroups.GroupName, or null when not set.</summary>
    public string? GroupName { get; init; }

    // ── Key parameters ────────────────────────────────────────────────────────────

    /// <summary>ระยะเวลาประเมิน (ปี) — source: IncomeAnalyses.TotalNumberOfYears.</summary>
    public int TotalNumberOfYears { get; init; }

    /// <summary>อัตราผลตอบแทน (Capitalization Rate) — source: IncomeAnalyses.CapitalizeRate (%).</summary>
    public decimal? CapitalizeRate { get; init; }

    /// <summary>อัตราคิดลด (Discount Rate) — source: IncomeAnalyses.DiscountedRate (%).</summary>
    public decimal? DiscountedRate { get; init; }

    // ── Year-indexed cashflow table ────────────────────────────────────────────────

    /// <summary>
    /// One row per year. Built by deserializing the five Summary_*Json arrays and zipping
    /// by year index (0 = year 1, up to TotalNumberOfYears entries).
    /// </summary>
    public IReadOnlyList<IncomeYearRow> YearRows { get; init; } = [];

    // ── HBU top-up block ──────────────────────────────────────────────────────────

    /// <summary>
    /// HBU (Highest and Best Use) top-up block applies when this flag is FALSE.
    /// Default in domain is <c>true</c> (no HBU top-up for standard DCF).
    /// When false, the land value (AreaWa * PricePerSqWa) is added to FinalValueAdjust
    /// to produce the effective appraisal price.
    /// Source: IncomeAnalyses.IsHighestBestUsed (bit, default 1).
    /// See SaveIncomeAnalysisCommandHandler.cs:147 — HBU land added when !IsHighestBestUsed.
    /// </summary>
    public bool IsHighestBestUsed { get; init; }

    /// <summary>HBU land area — ไร่. Source: HighestBestUsed_AreaRai.</summary>
    public decimal? HbuAreaRai { get; init; }

    /// <summary>HBU land area — งาน. Source: HighestBestUsed_AreaNgan.</summary>
    public decimal? HbuAreaNgan { get; init; }

    /// <summary>HBU land area — วา. Source: HighestBestUsed_AreaWa (18,2).</summary>
    public decimal? HbuAreaWa { get; init; }

    /// <summary>HBU price per sq.wa — บาท/ตร.วา. Source: HighestBestUsed_PricePerSqWa (18,2).</summary>
    public decimal? HbuPricePerSqWa { get; init; }

    // ── Final value (W1 parity) ───────────────────────────────────────────────────

    /// <summary>
    /// Pre-adjusted income value (before HBU top-up or override).
    /// Source: PricingFinalValues.FinalValueAdjusted (Phase C: moved from IncomeAnalyses).
    /// </summary>
    public decimal? FinalValueAdjust { get; init; }

    /// <summary>
    /// Explicit user override price.
    /// Source: PricingFinalValues.AppraisalPrice (Phase C: moved from IncomeAnalyses).
    /// When > 0, takes priority over all derived values.
    /// </summary>
    public decimal? AppraisalPriceRounded { get; init; }

    /// <summary>
    /// Raw DCF output value (fallback).
    /// Source: PricingFinalValues.FinalValueRounded (Phase C: moved from IncomeAnalyses).
    /// </summary>
    public decimal? FinalValueRounded { get; init; }

    /// <summary>
    /// Effective final value mirroring SaveIncomeAnalysisCommandHandler precedence:
    ///   AppraisalPriceRounded > 0  →  AppraisalPriceRounded
    ///   FinalValueAdjust.HasValue  →  FinalValueAdjust + HBU land value (when !IsHighestBestUsed)
    ///   else                       →  FinalValueRounded
    /// Computed in IncomeSectionLoader.LoadOneAsync.
    /// </summary>
    public decimal? EffectiveFinalValue { get; init; }
}

/// <summary>One year row in the Income DCF cashflow table.</summary>
public sealed class IncomeYearRow
{
    /// <summary>ปีที่ — 1-based year index.</summary>
    public int YearIndex { get; init; }

    /// <summary>รายได้รวม — source: element[YearIndex-1] of Summary_GrossRevenueJson.</summary>
    public decimal? GrossRevenue { get; init; }

    /// <summary>รายได้จากมูลค่าซากสุดท้าย — source: Summary_TerminalRevenueJson.</summary>
    public decimal? TerminalRevenue { get; init; }

    /// <summary>รายได้สุทธิ — source: Summary_TotalNetJson.</summary>
    public decimal? TotalNet { get; init; }

    /// <summary>ตัวคูณลด — source: Summary_DiscountJson.</summary>
    public decimal? Discount { get; init; }

    /// <summary>มูลค่าปัจจุบัน — source: Summary_PresentValueJson.</summary>
    public decimal? PresentValue { get; init; }
}
