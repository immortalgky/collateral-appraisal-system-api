namespace Reporting.Application.Models.Sections;

/// <summary>
/// Sub-model for the "วิธีสิทธิการเช่า (Leasehold)" pricing section.
///
/// Source: appraisal.LeaseholdAnalyses (FK: PricingMethodId, unique).
/// Confirmed against AddLeaseholdAnalysis migration:
///   Table = "LeaseholdAnalyses" (schema appraisal)
///   Inputs: LandValuePerSqWa (18,2), LandGrowthRateType (nvarchar 20),
///     LandGrowthRatePercent (10,4), LandGrowthIntervalYears (int),
///     ConstructionCostIndex (10,4), InitialBuildingValue (18,2),
///     DepreciationRate (10,4), DepreciationIntervalYears (int),
///     BuildingCalcStartYear (int), DiscountRate (10,4)
///   Computed: TotalIncomeOverLeaseTerm (18,2), ValueAtLeaseExpiry (18,2),
///     FinalValueRounded (18,2), IsPartialUsage (bit)
///   Partial usage: PartialRai, PartialNgan, PartialWa, PartialLandArea,
///     PricePerSqWa, PartialLandPrice, EstimateNetPrice, EstimatePriceRounded (all 18,2 nullable)
///
/// Child table: appraisal.LeaseholdCalculationDetails (FK: LeaseholdAnalysisId)
///   Confirmed against AddLeaseholdCalculationDetails migration:
///   DisplaySequence (int), Year (10,2), LandValue (18,2), LandGrowthPercent (10,4),
///   BuildingValue (18,2), DepreciationAmount (18,2), DepreciationPercent (10,4),
///   BuildingAfterDepreciation (18,2), TotalLandAndBuilding (18,2),
///   RentalIncome (18,2), PvFactor (18,10), NetCurrentRentalIncome (18,2)
/// </summary>
public sealed class LeaseholdSection
{
    // ── Property group routing ────────────────────────────────────────────────────

    /// <summary>PropertyGroups.GroupNumber for the group this Leasehold method belongs to.</summary>
    public int GroupNumber { get; init; }

    /// <summary>PropertyGroups.GroupName, or null when not set.</summary>
    public string? GroupName { get; init; }

    // ── Inputs ────────────────────────────────────────────────────────────────────

    /// <summary>มูลค่าที่ดินต่อตารางวา (บาท) — source: LandValuePerSqWa (18,2).</summary>
    public decimal? LandValuePerSqWa { get; init; }

    /// <summary>ประเภทอัตราการเติบโต — "Frequency" หรือ "Period".</summary>
    public string? LandGrowthRateType { get; init; }

    /// <summary>อัตราการเติบโตที่ดิน (%) — source: LandGrowthRatePercent (10,4).</summary>
    public decimal? LandGrowthRatePercent { get; init; }

    /// <summary>ช่วงเวลาการเติบโต (ปี) — source: LandGrowthIntervalYears (int).</summary>
    public int? LandGrowthIntervalYears { get; init; }

    /// <summary>ดัชนีต้นทุนก่อสร้าง — source: ConstructionCostIndex (10,4).</summary>
    public decimal? ConstructionCostIndex { get; init; }

    /// <summary>มูลค่าสิ่งปลูกสร้างตั้งต้น (บาท) — source: InitialBuildingValue (18,2).</summary>
    public decimal? InitialBuildingValue { get; init; }

    /// <summary>อัตราค่าเสื่อมราคา (%) — source: DepreciationRate (10,4).</summary>
    public decimal? DepreciationRate { get; init; }

    /// <summary>ช่วงเวลาค่าเสื่อม (ปี) — source: DepreciationIntervalYears (int).</summary>
    public int? DepreciationIntervalYears { get; init; }

    /// <summary>ปีเริ่มต้นคำนวณอาคาร — source: BuildingCalcStartYear (int).</summary>
    public int? BuildingCalcStartYear { get; init; }

    /// <summary>อัตราคิดลด (%) — source: DiscountRate (10,4).</summary>
    public decimal? DiscountRate { get; init; }

    // ── Calculation table ─────────────────────────────────────────────────────────

    /// <summary>
    /// Per-year calculation rows ordered by DisplaySequence.
    /// Source: appraisal.LeaseholdCalculationDetails.
    /// </summary>
    public IReadOnlyList<LeaseholdCalcRow> TableRows { get; init; } = [];

    // ── Totals ────────────────────────────────────────────────────────────────────

    /// <summary>รายได้รวมตลอดอายุสัญญา — source: TotalIncomeOverLeaseTerm (18,2).</summary>
    public decimal? TotalIncomeOverLeaseTerm { get; init; }

    /// <summary>มูลค่า ณ สิ้นสุดสัญญา — source: ValueAtLeaseExpiry (18,2).</summary>
    public decimal? ValueAtLeaseExpiry { get; init; }

    /// <summary>DCF calculated value (before user override) — source: FinalValueRounded (18,2).</summary>
    public decimal? FinalValueRounded { get; init; }

    /// <summary>
    /// Effective final value — mirrors SaveLeaseholdAnalysisCommandHandler.cs:153-155:
    ///   EstimatePriceRounded ?? (computedEstimatePriceRounded ?? FinalValueRounded)
    /// For non-partial: computedEstimatePriceRounded = null, so effective = EstimatePriceRounded ?? FinalValueRounded.
    /// For partial: effective = EstimatePriceRounded ?? partialEstimate.
    /// Computed in LeaseholdSectionLoader.LoadOneAsync.
    /// </summary>
    public decimal? EffectiveFinalValue { get; init; }

    // ── Partial usage block ───────────────────────────────────────────────────────

    /// <summary>True when partial land area is used — gates the partial usage block.</summary>
    public bool IsPartialUsage { get; init; }

    /// <summary>ไร่. Source: PartialRai (18,2 nullable).</summary>
    public decimal? PartialRai { get; init; }

    /// <summary>งาน. Source: PartialNgan (18,2 nullable).</summary>
    public decimal? PartialNgan { get; init; }

    /// <summary>ตารางวา. Source: PartialWa (18,2 nullable).</summary>
    public decimal? PartialWa { get; init; }

    /// <summary>เนื้อที่ (ตร.วา) — source: PartialLandArea (18,2 nullable).</summary>
    public decimal? PartialLandArea { get; init; }

    /// <summary>ราคาต่อตารางวา — source: PricePerSqWa (18,2 nullable).</summary>
    public decimal? PricePerSqWa { get; init; }

    /// <summary>มูลค่าที่ดินส่วน — source: PartialLandPrice (18,2 nullable).</summary>
    public decimal? PartialLandPrice { get; init; }

    /// <summary>มูลค่าสิทธิการเช่าสุทธิ — source: EstimateNetPrice (18,2 nullable).</summary>
    public decimal? EstimateNetPrice { get; init; }

    /// <summary>มูลค่าสิทธิการเช่า (ปัดเศษ) — source: EstimatePriceRounded (18,2 nullable).</summary>
    public decimal? EstimatePriceRounded { get; init; }
}

/// <summary>One year row in the Leasehold calculation table.</summary>
public sealed class LeaseholdCalcRow
{
    public int DisplaySequence { get; init; }

    /// <summary>ปีที่ — source: Year (decimal 10,2).</summary>
    public decimal? Year { get; init; }

    /// <summary>มูลค่าที่ดิน — source: LandValue (18,2).</summary>
    public decimal? LandValue { get; init; }

    /// <summary>อัตราการเติบโต (%) — source: LandGrowthPercent (10,4).</summary>
    public decimal? LandGrowthPercent { get; init; }

    /// <summary>มูลค่าอาคาร — source: BuildingValue (18,2).</summary>
    public decimal? BuildingValue { get; init; }

    /// <summary>ค่าเสื่อมราคา — source: DepreciationAmount (18,2).</summary>
    public decimal? DepreciationAmount { get; init; }

    /// <summary>อัตราค่าเสื่อม (%) — source: DepreciationPercent (10,4).</summary>
    public decimal? DepreciationPercent { get; init; }

    /// <summary>มูลค่าอาคารหลังค่าเสื่อม — source: BuildingAfterDepreciation (18,2).</summary>
    public decimal? BuildingAfterDepreciation { get; init; }

    /// <summary>รวมที่ดินและอาคาร — source: TotalLandAndBuilding (18,2).</summary>
    public decimal? TotalLandAndBuilding { get; init; }

    /// <summary>รายได้ค่าเช่า — source: RentalIncome (18,2).</summary>
    public decimal? RentalIncome { get; init; }

    /// <summary>ตัวคูณลด — source: PvFactor (18,10).</summary>
    public decimal? PvFactor { get; init; }

    /// <summary>รายได้ค่าเช่าปัจจุบันสุทธิ — source: NetCurrentRentalIncome (18,2).</summary>
    public decimal? NetCurrentRentalIncome { get; init; }
}
