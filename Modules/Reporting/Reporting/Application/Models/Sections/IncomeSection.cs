namespace Reporting.Application.Models.Sections;

/// <summary>
/// Sub-model for the "วิธีรายได้ (DCF)" Income / Discounted Cash Flow pricing section.
///
/// Source: appraisal.IncomeAnalyses (FK: PricingAnalysisMethodId).
/// Sections → categories → assumptions (appraisal.IncomeSections/Categories/Assumptions) and the
/// Summary_*Json arrays are laid out as a line-item cash-flow table in <see cref="CashFlowBlocks"/>.
/// Confirmed against IncomeAnalysisConfiguration.cs:
///   Table = "IncomeAnalyses" (schema appraisal)
///   FK column: PricingAnalysisMethodId (unique index)
///   Scalar columns: TotalNumberOfYears (int), CapitalizeRate (5,2), DiscountedRate (5,2),
///     IsHighestBestUsed (bit); FinalValueRounded/IndicatedValue come from PricingFinalValues
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

    /// <summary>Which approach / role this page belongs to and whether it is a reference (printed in the page banner).</summary>
    public PricingMethodContext? Context { get; init; }

    // ── Key parameters ────────────────────────────────────────────────────────────

    /// <summary>ระยะเวลาประเมิน (ปี) — source: IncomeAnalyses.TotalNumberOfYears.</summary>
    public int TotalNumberOfYears { get; init; }

    /// <summary>อัตราผลตอบแทน (Capitalization Rate) — source: IncomeAnalyses.CapitalizeRate (%).</summary>
    public decimal? CapitalizeRate { get; init; }

    /// <summary>อัตราคิดลด (Discount Rate) — source: IncomeAnalyses.DiscountedRate (%).</summary>
    public decimal? DiscountedRate { get; init; }

    /// <summary>IncomeAnalyses.TemplateName — the template snapshot the analysis was built from (e.g. "DCF - Hotel").</summary>
    public string? TemplateName { get; init; }

    /// <summary>IncomeAnalyses.TotalNumberOfDayInYear.</summary>
    public int DaysInYear { get; init; }

    /// <summary>
    /// Direct Capitalization instead of DCF. Same rule as IncomeCalculationService.ComputeDcfSummary
    /// (a summaryDirect section or a 1-year analysis), plus a "direct-*" template code.
    /// Direct: one year, value = NOI ÷ Cap Rate, no discount rate.
    /// </summary>
    public bool IsDirectCapitalization { get; init; }

    // ── Line-item cash-flow table ───────────────────────────────────────────────────

    /// <summary>The cash-flow table split into blocks of at most 6 years (label column repeats per block).</summary>
    public IReadOnlyList<IncomeCashFlowBlock> CashFlowBlocks { get; init; } = [];

    /// <summary>Net income of the last year (Summary_GrossRevenueJson[N-1]) — the base of the terminal value (DCF only).</summary>
    public decimal? TerminalBaseNoi { get; init; }

    /// <summary>Summary_TerminalRevenueJson[N-2] — the terminal value, discounted in year N-1 (DCF only).</summary>
    public decimal? TerminalValue { get; init; }

    /// <summary>Direct: Summary_TotalNetJson[0] (the NOI) — DCF: null.</summary>
    public decimal? DirectNoi { get; init; }

    /// <summary>Σ Summary_PresentValueJson — the screen's "มูลค่าจากรายได้ (ผลรวม PV)". Direct: the single NOI ÷ Cap Rate value.</summary>
    public decimal? PresentValueTotal { get; init; }

    /// <summary>One row per assumption, in table order: name / method / one-line summary of its inputs.</summary>
    public IReadOnlyList<IncomeAssumptionLine> Assumptions { get; init; } = [];

    /// <summary>Land-value (SubjectType 3) and room-rate (SubjectType 5) sub-analyses hosted by this method — partials/book-sub-analyses.</summary>
    public IReadOnlyList<PricingSubAnalysis> SubAnalyses { get; init; } = [];

    /// <summary>HBU area via ThaiLandAreaFormatter.FormatTotal ("1 - 0 - 0 ไร่ หรือ 400 ตารางวา"); null when rai, ngan and wa are all empty.</summary>
    public string? HbuAreaText { get; init; }

    /// <summary>HBU land value = (Rai×400 + Ngan×100 + Wa) × price per sq.wa; null when the HBU block does not apply.</summary>
    public decimal? HbuLandValue { get; init; }

    /// <summary>
    /// รวม — income value (Sum of PV) + the split-off land value, as the screen's HBU = No "= Total" row. Null unless HBU is
    /// "No" with a land value and a Sum of PV. Independent of any figure the appraiser typed.
    /// </summary>
    public decimal? HbuTotalValue { get; init; }

    /// <summary>
    /// The appraiser typed the value (IndicatedValue) and it is not the system figure rounded to the thousand — the screen's
    /// "Edited" test. The system figure is the final value rounded (HBU) or floor(adjusted value + split land) (HBU = No).
    /// </summary>
    public bool IsValueEdited { get; init; }

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
    /// Source: PricingFinalValues.FinalValueOverride (Phase C: moved from IncomeAnalyses).
    /// </summary>
    public decimal? FinalValueAdjust { get; init; }

    /// <summary>
    /// Explicit user override price; null means the appraiser did not override.
    /// Source: PricingFinalValues.IndicatedValue (Phase C: moved from IncomeAnalyses).
    /// When set, takes priority over all derived values.
    /// </summary>
    public decimal? IndicatedValue { get; init; }

    /// <summary>
    /// Raw DCF output value (fallback).
    /// Source: PricingFinalValues.FinalValueRounded (Phase C: moved from IncomeAnalyses).
    /// </summary>
    public decimal? FinalValueRounded { get; init; }

    /// <summary>
    /// Effective final value mirroring SaveIncomeAnalysisCommandHandler precedence:
    ///   IndicatedValue.HasValue    →  IndicatedValue
    ///   FinalValueAdjust.HasValue  →  FinalValueAdjust + HBU land value (when !IsHighestBestUsed)
    ///   else                       →  FinalValueRounded
    /// Computed in IncomeSectionLoader.LoadOneAsync.
    /// </summary>
    public decimal? EffectiveFinalValue { get; init; }
}

/// <summary>A block of up to 6 year columns of the cash-flow table.</summary>
public sealed class IncomeCashFlowBlock
{
    /// <summary>1-based year numbers of the columns in this block.</summary>
    public IReadOnlyList<int> Years { get; init; } = [];

    public IReadOnlyList<IncomeCashFlowRow> Rows { get; init; } = [];
}

/// <summary>One row of the cash-flow table.</summary>
public sealed class IncomeCashFlowRow
{
    public string Label { get; init; } = "";

    /// <summary>sec = section header · cat = category header · item = assumption · sub = category subtotal · total = bold total.</summary>
    public string Kind { get; init; } = "item";

    /// <summary>Scriban math.format pattern for the cells (N2 money, N6 discount factor).</summary>
    public string Format { get; init; } = "N2";

    /// <summary>One cell per year column of the block; null prints "-". Empty for sec/cat header rows.</summary>
    public IReadOnlyList<decimal?> Cells { get; init; } = [];
}

/// <summary>One row of the assumption table.</summary>
public sealed class IncomeAssumptionLine
{
    public int Number { get; init; }
    public string Name { get; init; } = "";
    /// <summary>Method code + Thai short name, e.g. "13 สัดส่วน".</summary>
    public string Method { get; init; } = "";
    /// <summary>Thai one-liner of the key inputs of Method_DetailJson; null when nothing was entered.</summary>
    public string? Summary { get; init; }
}
