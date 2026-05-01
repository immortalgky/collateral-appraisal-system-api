namespace Appraisal.Domain.Appraisals.Hypothesis.Summaries;

/// <summary>
/// Owned-entity summary block for the Land and Building variant.
/// Stores user-input fields and server-computed outputs for C01..C82.
/// Per-model aggregates (A01..A10, per-model C11-C26) are derived at calculation time
/// from LandBuildingUnitRow data and are not persisted here.
/// </summary>
public class LandBuildingSummary
{
    // ── Details of the Assessed Land Area ─────────────────────────────────
    /// <summary>C01 — Total land area (Sq.Wa). Auto from title deed.</summary>
    public decimal? C01TotalArea { get; set; }

    /// <summary>C02 — Selling area as N% of total area (user input, %).</summary>
    public decimal? C02SellingAreaPercent { get; set; }

    /// <summary>C03 — Selling area (Sq.Wa). Computed: SUM of per-model total land areas.</summary>
    public decimal? C03SellingArea { get; set; }

    /// <summary>C10 — Public utility area as N% of total area (user input, %).</summary>
    public decimal? C10PublicUtilityAreaPercent { get; set; }

    /// <summary>C10A — Public utility area (Sq.Wa). Computed: C10 × C01.</summary>
    public decimal? C10APublicUtilityArea { get; set; }

    // ── Project Revenue Estimates ──────────────────────────────────────────
    /// <summary>C15 — Total project revenue (Baht). Computed: SUM of per-model selling prices.</summary>
    public decimal? C15TotalRevenue { get; set; }

    // ── Estimate Sales Period ──────────────────────────────────────────────
    /// <summary>C16 — Estimated sales units per month (user input).</summary>
    public int? C16EstSalesPeriod { get; set; }

    /// <summary>C17 — Total units (computed: SUM of per-model units).</summary>
    public int? C17TotalUnits { get; set; }

    /// <summary>C18 — Estimated duration in months. Computed: C17 / C16.</summary>
    public int? C18EstimatedDurationMonths { get; set; }

    // ── Project Dev Cost (Public Utility / Land Filling) ──────────────────
    /// <summary>C27 — Public utility construction cost (Baht/SqWa). User input.</summary>
    public decimal? C27PublicUtilityRatePerSqWa { get; set; }

    /// <summary>C28 — Public utility area (SqWa). Computed: C01.</summary>
    public decimal? C28PublicUtilityArea { get; set; }

    /// <summary>C29 — Public utility cost (Baht). Computed: C27 × C28.</summary>
    public decimal? C29PublicUtilityCost { get; set; }

    /// <summary>C30 — Public utility cost ratio (%). Computed.</summary>
    public decimal? C30PublicUtilityCostRatio { get; set; }

    /// <summary>C31 — Land filling cost (Baht/SqWa). User input.</summary>
    public decimal? C31LandFillingRatePerSqWa { get; set; }

    /// <summary>C32 — Land filling area (SqWa). Computed: C01.</summary>
    public decimal? C32LandFillingArea { get; set; }

    /// <summary>C33 — Land filling cost (Baht). Computed: C31 × C32.</summary>
    public decimal? C33LandFillingCost { get; set; }

    /// <summary>C34 — Land filling cost ratio (%). Computed.</summary>
    public decimal? C34LandFillingCostRatio { get; set; }

    /// <summary>C35 — Contingency allowance (% of project dev costs). Default 3%.</summary>
    public decimal? C35ContingencyPercent { get; set; }

    /// <summary>C36 — Contingency allowance (Baht). Computed: (C21+C25+C29+C33) × C35.</summary>
    public decimal? C36ContingencyAmount { get; set; }

    /// <summary>C37 — Contingency ratio (%). Computed.</summary>
    public decimal? C37ContingencyRatio { get; set; }

    /// <summary>C38 — Total project dev cost (Baht). Computed: C21+C25+C29+C33+C36.</summary>
    public decimal? C38TotalProjectDevCost { get; set; }

    /// <summary>C39 — Total dev cost ratio (%). Computed as 100%.</summary>
    public decimal? C39TotalDevCostRatio { get; set; }

    // ── Estimate Construction Period ───────────────────────────────────────
    /// <summary>C40 — Estimated construction units per month (user input).</summary>
    public int? C40EstConstructionPeriod { get; set; }

    /// <summary>C41 — Total units (= C17).</summary>
    public int? C41TotalUnits { get; set; }

    /// <summary>C42 — Estimated duration (months). Computed: C41 / C40.</summary>
    public int? C42EstimatedDurationMonths { get; set; }

    // ── Project Cost fields (computed from cost items + user inputs) ───────
    /// <summary>C44 — Allocation permit fee (Baht). = C43 (user).</summary>
    public decimal? C44AllocationPermitFee { get; set; }

    /// <summary>C45 — Allocation permit fee ratio (%). Computed: (C44×100)/C64.</summary>
    public decimal? C45AllocationPermitFeeRatio { get; set; }

    /// <summary>C46 — Land title deed division fee per plot (Baht). User input.</summary>
    public decimal? C46LandTitleFeePerPlot { get; set; }

    /// <summary>C47 — Total plots. = C41.</summary>
    public int? C47TotalPlots { get; set; }

    /// <summary>C48 — Land title deed division fee total (Baht). Computed: C46×C47.</summary>
    public decimal? C48LandTitleFeeTotal { get; set; }

    /// <summary>C49 — Land title deed fee ratio (%). Computed.</summary>
    public decimal? C49LandTitleFeeRatio { get; set; }

    /// <summary>C50 — Professional service fees (Baht/Month). User input.</summary>
    public decimal? C50ProfessionalFeePerMonth { get; set; }

    /// <summary>C51 — Professional fee months. = C42.</summary>
    public int? C51ProfessionalFeeMonths { get; set; }

    /// <summary>C52 — Professional fee total (Baht). Computed: C50×C51.</summary>
    public decimal? C52ProfessionalFeeTotal { get; set; }

    /// <summary>C53 — Professional fee ratio (%). Computed.</summary>
    public decimal? C53ProfessionalFeeRatio { get; set; }

    /// <summary>C54 — Project admin cost (Baht/Month). User input.</summary>
    public decimal? C54AdminCostPerMonth { get; set; }

    /// <summary>C55 — Admin cost months. = C18.</summary>
    public int? C55AdminCostMonths { get; set; }

    /// <summary>C56 — Admin cost total (Baht). Computed: C54×C55.</summary>
    public decimal? C56AdminCostTotal { get; set; }

    /// <summary>C57 — Admin cost ratio (%). Computed.</summary>
    public decimal? C57AdminCostRatio { get; set; }

    /// <summary>C58 — Selling/Adv expenses (N% of revenue). User input.</summary>
    public decimal? C58SellingAdvPercent { get; set; }

    /// <summary>C59 — Selling/Adv total (Baht). Computed: C15×C58.</summary>
    public decimal? C59SellingAdvTotal { get; set; }

    /// <summary>C60 — Selling/Adv ratio (%). Computed.</summary>
    public decimal? C60SellingAdvRatio { get; set; }

    /// <summary>C61 — Contingency (% of project costs). Default 3%.</summary>
    public decimal? C61ProjectContingencyPercent { get; set; }

    /// <summary>C62 — Contingency (Baht). Computed: (C44+C48+C52+C56+C59)×C61/100.</summary>
    public decimal? C62ProjectContingencyAmount { get; set; }

    /// <summary>C63 — Contingency ratio (%). Computed.</summary>
    public decimal? C63ProjectContingencyRatio { get; set; }

    /// <summary>C64 — Total project cost (Baht). Computed: C44+C48+C52+C56+C59+C62.</summary>
    public decimal? C64TotalProjectCost { get; set; }

    /// <summary>C65 — Total project cost ratio (%). Computed: 100%.</summary>
    public decimal? C65TotalProjectCostRatio { get; set; }

    // ── Government Taxes ──────────────────────────────────────────────────
    /// <summary>C66 — Transfer fee (N% of revenue). User input.</summary>
    public decimal? C66TransferFeePercent { get; set; }

    /// <summary>C67 — Transfer fee (Baht). Computed: C15×C66.</summary>
    public decimal? C67TransferFeeAmount { get; set; }

    /// <summary>C68 — Transfer fee govt ratio (%). Computed: (C67×100)/C72.</summary>
    public decimal? C68TransferFeeRatio { get; set; }

    /// <summary>C69 — Specific business tax (N% of revenue). User input.</summary>
    public decimal? C69SpecificBizTaxPercent { get; set; }

    /// <summary>C70 — Specific business tax (Baht). Computed: C15×C69.</summary>
    public decimal? C70SpecificBizTaxAmount { get; set; }

    /// <summary>C71 — Specific biz tax ratio (%). Computed.</summary>
    public decimal? C71SpecificBizTaxRatio { get; set; }

    /// <summary>C72 — Total govt taxes (Baht). Computed: C67+C70.</summary>
    public decimal? C72TotalGovTax { get; set; }

    /// <summary>C73 — Total govt tax ratio (%). Computed: 100%.</summary>
    public decimal? C73TotalGovTaxRatio { get; set; }

    // ── Risk Premium ──────────────────────────────────────────────────────
    /// <summary>C74 — Risk premium (N% of revenue). User input.</summary>
    public decimal? C74RiskPremiumPercent { get; set; }

    /// <summary>C75 — Risk premium (Baht). Computed: C15×C74.</summary>
    public decimal? C75RiskPremiumAmount { get; set; }

    // ── Including Dev Costs ────────────────────────────────────────────────
    /// <summary>C76 — Total development costs incl all. Computed: C38+C64+C72+C75.</summary>
    public decimal? C76TotalDevCostsAndExpenses { get; set; }

    // ── Current Property Value ─────────────────────────────────────────────
    /// <summary>C77 — Current property value.
    /// If C78=0: C77 = C15 - C76.
    /// If C78≠0: C77 = (C15 - C76) × C78 (i.e., C78 is a % applied as a factor).
    /// </summary>
    public decimal? C77CurrentPropertyValue { get; set; }

    /// <summary>C78 — Discount rate (%). User input. 0 means no discounting.</summary>
    public decimal? C78DiscountRate { get; set; }

    /// <summary>C79 — Discount rate factor. Computed: 1 / (1 + C78/100)^(C18/12).</summary>
    public decimal? C79DiscountRateFactor { get; set; }

    /// <summary>C80 — Final property value (Baht). Computed: C77 × C79.</summary>
    public decimal? C80FinalPropertyValue { get; set; }

    /// <summary>C81 — Total asset value rounded to nearest 10,000.</summary>
    public decimal? C81TotalAssetValueRounded { get; set; }

    /// <summary>C82 — Total asset value per Sq.Wa, rounded to nearest 100. = C81/C01.</summary>
    public decimal? C82TotalAssetValuePerSqWa { get; set; }

    public string? Remark { get; set; }
}
