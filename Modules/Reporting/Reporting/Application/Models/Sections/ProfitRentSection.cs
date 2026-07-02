namespace Reporting.Application.Models.Sections;

/// <summary>
/// Sub-model for the "วิธีค่าเช่า (Profit Rent)" pricing section.
///
/// Source: appraisal.ProfitRentAnalyses (FK: PricingMethodId, unique).
/// Confirmed against AddProfitRentAnalysis migration:
///   Table = "ProfitRentAnalyses" (schema appraisal)
///   Inputs: MarketRentalFeePerSqWa (18,2), GrowthRateType (nvarchar 20),
///     GrowthRatePercent (10,4), GrowthIntervalYears (int), DiscountRate (10,4)
///   Computed: TotalPresentValue (18,2), FinalValueRounded (18,2)
///
/// Child table: appraisal.ProfitRentCalculationDetails (FK: ProfitRentAnalysisId)
///   Columns confirmed against AddProfitRentAnalysis migration:
///   DisplaySequence (int), Year (10,2), NumberOfMonths (10,2),
///   MarketRentalFeePerSqWa (18,2), MarketRentalFeeGrowthPercent (10,4),
///   MarketRentalFeePerMonth (18,2), MarketRentalFeePerYear (18,2),
///   ContractRentalFeePerYear (18,2), ReturnsFromLease (18,2),
///   PvFactor (18,10), PresentValue (18,2)
/// </summary>
public sealed class ProfitRentSection
{
    // ── Property group routing ────────────────────────────────────────────────────

    /// <summary>PropertyGroups.GroupNumber for the group this ProfitRent method belongs to.</summary>
    public int GroupNumber { get; init; }

    /// <summary>PropertyGroups.GroupName, or null when not set.</summary>
    public string? GroupName { get; init; }

    // ── Inputs ────────────────────────────────────────────────────────────────────

    /// <summary>ค่าเช่าตลาดต่อตารางวา (บาท) — source: MarketRentalFeePerSqWa (18,2).</summary>
    public decimal? MarketRentalFeePerSqWa { get; init; }

    /// <summary>ประเภทอัตราการเติบโต — "Frequency" หรือ "Period".</summary>
    public string? GrowthRateType { get; init; }

    /// <summary>อัตราการเติบโต (%) — source: GrowthRatePercent (10,4).</summary>
    public decimal? GrowthRatePercent { get; init; }

    /// <summary>ช่วงเวลาการเติบโต (ปี) — source: GrowthIntervalYears (int).</summary>
    public int? GrowthIntervalYears { get; init; }

    /// <summary>อัตราคิดลด (%) — source: DiscountRate (10,4).</summary>
    public decimal? DiscountRate { get; init; }

    // ── Calculation table ─────────────────────────────────────────────────────────

    /// <summary>
    /// Per-period calculation rows ordered by DisplaySequence.
    /// Source: appraisal.ProfitRentCalculationDetails.
    /// </summary>
    public IReadOnlyList<ProfitRentCalcRow> TableRows { get; init; } = [];

    // ── Totals ────────────────────────────────────────────────────────────────────

    /// <summary>รวมมูลค่าปัจจุบัน — source: TotalPresentValue (18,2).</summary>
    public decimal? TotalPresentValue { get; init; }

    /// <summary>DCF calculated value (before user override) — source: FinalValueRounded (18,2).</summary>
    public decimal? FinalValueRounded { get; init; }

    /// <summary>
    /// User-override price stored on the entity.
    /// Source: ProfitRentAnalyses.EstimatePriceRounded (18,2 nullable).
    /// Confirmed: ProfitRentAnalysis.cs line 24.
    /// </summary>
    public decimal? EstimatePriceRounded { get; init; }

    /// <summary>
    /// Effective final value — mirrors SaveProfitRentAnalysisCommandHandler.cs:110:
    ///   EstimatePriceRounded ?? FinalValueRounded
    /// Computed in ProfitRentSectionLoader.LoadOneAsync.
    /// </summary>
    public decimal? EffectiveFinalValue { get; init; }
}

/// <summary>One period row in the Profit Rent calculation table.</summary>
public sealed class ProfitRentCalcRow
{
    public int DisplaySequence { get; init; }

    /// <summary>ปีที่ / ช่วงปีที่ — source: Year (decimal 10,2).</summary>
    public decimal? Year { get; init; }

    /// <summary>จำนวนเดือน — source: NumberOfMonths (decimal 10,2).</summary>
    public decimal? NumberOfMonths { get; init; }

    /// <summary>ค่าเช่าตลาด ต่อตารางวา — source: MarketRentalFeePerSqWa (18,2).</summary>
    public decimal? MarketRentalFeePerSqWa { get; init; }

    /// <summary>อัตราการเติบโต (%) — source: MarketRentalFeeGrowthPercent (10,4).</summary>
    public decimal? MarketRentalFeeGrowthPercent { get; init; }

    /// <summary>ค่าเช่าตลาดต่อเดือน — source: MarketRentalFeePerMonth (18,2).</summary>
    public decimal? MarketRentalFeePerMonth { get; init; }

    /// <summary>ค่าเช่าตลาดต่อปี — source: MarketRentalFeePerYear (18,2).</summary>
    public decimal? MarketRentalFeePerYear { get; init; }

    /// <summary>ค่าเช่าตามสัญญาต่อปี — source: ContractRentalFeePerYear (18,2).</summary>
    public decimal? ContractRentalFeePerYear { get; init; }

    /// <summary>ผลตอบแทนจากการเช่า — source: ReturnsFromLease (18,2).</summary>
    public decimal? ReturnsFromLease { get; init; }

    /// <summary>ตัวคูณลด — source: PvFactor (18,10).</summary>
    public decimal? PvFactor { get; init; }

    /// <summary>มูลค่าปัจจุบัน — source: PresentValue (18,2).</summary>
    public decimal? PresentValue { get; init; }
}
