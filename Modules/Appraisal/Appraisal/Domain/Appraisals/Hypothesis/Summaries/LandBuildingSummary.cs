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
    /// <summary>FSD field C01: Total land area (Sq.Wa). Auto from title deed.</summary>
    public decimal? TotalArea { get; set; }

    /// <summary>FSD field C02: Selling area as N% of total area (user input, %).</summary>
    public decimal? SellingAreaPercent { get; set; }

    /// <summary>FSD field C03: Selling area (Sq.Wa). Computed: SUM of per-model total land areas.</summary>
    public decimal? SellingArea { get; set; }

    /// <summary>FSD field C10: Public utility area as N% of total area (user input, %).</summary>
    public decimal? PublicUtilityAreaPercent { get; set; }

    /// <summary>FSD field C10A: Public utility area (Sq.Wa). Computed: C10 × C01.</summary>
    public decimal? PublicUtilityArea { get; set; }

    // ── Project Revenue Estimates ──────────────────────────────────────────
    /// <summary>FSD field C15: Total project revenue (Baht). Computed: SUM of per-model selling prices.</summary>
    public decimal? TotalRevenue { get; set; }

    // ── Estimate Sales Period ──────────────────────────────────────────────
    /// <summary>FSD field C16: Estimated sales units per month (user input).</summary>
    public int? EstSalesPeriod { get; set; }

    /// <summary>FSD field C17: Total units (computed: SUM of per-model units).</summary>
    public int? TotalUnits { get; set; }

    /// <summary>FSD field C18: Estimated duration in months. Computed: C17 / C16.</summary>
    public int? EstimatedDurationMonths { get; set; }

    // ── Project Dev Cost (Public Utility / Land Filling) ──────────────────
    /// <summary>FSD field C27: Public utility construction cost (Baht/SqWa). User input.</summary>
    public decimal? PublicUtilityRatePerSqWa { get; set; }

    /// <summary>FSD field C28: Public utility area (SqWa). Computed: C01.</summary>
    public decimal? PublicUtilityAreaForCost { get; set; }

    /// <summary>FSD field C29: Public utility cost (Baht). Computed: C27 × C28.</summary>
    public decimal? PublicUtilityCost { get; set; }

    /// <summary>FSD field C30: Public utility cost ratio (%). Computed.</summary>
    public decimal? PublicUtilityCostRatio { get; set; }

    /// <summary>FSD field C31: Land filling cost (Baht/SqWa). User input.</summary>
    public decimal? LandFillingRatePerSqWa { get; set; }

    /// <summary>FSD field C32: Land filling area (SqWa). Computed: C01.</summary>
    public decimal? LandFillingArea { get; set; }

    /// <summary>FSD field C33: Land filling cost (Baht). Computed: C31 × C32.</summary>
    public decimal? LandFillingCost { get; set; }

    /// <summary>FSD field C34: Land filling cost ratio (%). Computed.</summary>
    public decimal? LandFillingCostRatio { get; set; }

    /// <summary>FSD field C35: Contingency allowance (% of project dev costs). Default 3%.</summary>
    public decimal? ContingencyPercent { get; set; }

    /// <summary>FSD field C36: Contingency allowance (Baht). Computed: (C21+C25+C29+C33) × C35.</summary>
    public decimal? ContingencyAmount { get; set; }

    /// <summary>FSD field C37: Contingency ratio (%). Computed.</summary>
    public decimal? ContingencyRatio { get; set; }

    /// <summary>FSD field C38: Total project dev cost (Baht). Computed: C21+C25+C29+C33+C36.</summary>
    public decimal? TotalProjectDevCost { get; set; }

    /// <summary>FSD field C39: Total dev cost ratio (%). Computed as 100%.</summary>
    public decimal? TotalDevCostRatio { get; set; }

    // ── Estimate Construction Period ───────────────────────────────────────
    /// <summary>FSD field C40: Estimated construction units per month (user input).</summary>
    public int? EstConstructionPeriod { get; set; }

    /// <summary>FSD field C41: Total units (= C17).</summary>
    public int? TotalUnitsForConstruction { get; set; }

    /// <summary>FSD field C42: Estimated duration (months). Computed: C41 / C40.</summary>
    public int? EstimatedConstructionDurationMonths { get; set; }

    // ── Project Cost fields (computed from cost items + user inputs) ───────
    /// <summary>FSD field C44: Allocation permit fee (Baht). = C43 (user).</summary>
    public decimal? AllocationPermitFee { get; set; }

    /// <summary>FSD field C45: Allocation permit fee ratio (%). Computed: (C44×100)/C64.</summary>
    public decimal? AllocationPermitFeeRatio { get; set; }

    /// <summary>FSD field C46: Land title deed division fee per plot (Baht). User input.</summary>
    public decimal? LandTitleFeePerPlot { get; set; }

    /// <summary>FSD field C47: Total plots. = C41.</summary>
    public int? TotalPlots { get; set; }

    /// <summary>FSD field C48: Land title deed division fee total (Baht). Computed: C46×C47.</summary>
    public decimal? LandTitleFeeTotal { get; set; }

    /// <summary>FSD field C49: Land title deed fee ratio (%). Computed.</summary>
    public decimal? LandTitleFeeRatio { get; set; }

    /// <summary>FSD field C50: Professional service fees (Baht/Month). User input.</summary>
    public decimal? ProfessionalFeePerMonth { get; set; }

    /// <summary>FSD field C51: Professional fee months. = C42.</summary>
    public int? ProfessionalFeeMonths { get; set; }

    /// <summary>FSD field C52: Professional fee total (Baht). Computed: C50×C51.</summary>
    public decimal? ProfessionalFeeTotal { get; set; }

    /// <summary>FSD field C53: Professional fee ratio (%). Computed.</summary>
    public decimal? ProfessionalFeeRatio { get; set; }

    /// <summary>FSD field C54: Project admin cost (Baht/Month). User input.</summary>
    public decimal? AdminCostPerMonth { get; set; }

    /// <summary>FSD field C55: Admin cost months. = C18.</summary>
    public int? AdminCostMonths { get; set; }

    /// <summary>FSD field C56: Admin cost total (Baht). Computed: C54×C55.</summary>
    public decimal? AdminCostTotal { get; set; }

    /// <summary>FSD field C57: Admin cost ratio (%). Computed.</summary>
    public decimal? AdminCostRatio { get; set; }

    /// <summary>FSD field C58: Selling/Adv expenses (N% of revenue). User input.</summary>
    public decimal? SellingAdvPercent { get; set; }

    /// <summary>FSD field C59: Selling/Adv total (Baht). Computed: C15×C58.</summary>
    public decimal? SellingAdvTotal { get; set; }

    /// <summary>FSD field C60: Selling/Adv ratio (%). Computed.</summary>
    public decimal? SellingAdvRatio { get; set; }

    /// <summary>FSD field C61: Contingency (% of project costs). Default 3%.</summary>
    public decimal? ProjectContingencyPercent { get; set; }

    /// <summary>FSD field C62: Contingency (Baht). Computed: (C44+C48+C52+C56+C59)×C61/100.</summary>
    public decimal? ProjectContingencyAmount { get; set; }

    /// <summary>FSD field C63: Contingency ratio (%). Computed.</summary>
    public decimal? ProjectContingencyRatio { get; set; }

    /// <summary>FSD field C64: Total project cost (Baht). Computed: C44+C48+C52+C56+C59+C62.</summary>
    public decimal? TotalProjectCost { get; set; }

    /// <summary>FSD field C65: Total project cost ratio (%). Computed: 100%.</summary>
    public decimal? TotalProjectCostRatio { get; set; }

    // ── Government Taxes ──────────────────────────────────────────────────
    /// <summary>FSD field C66: Transfer fee (N% of revenue). User input.</summary>
    public decimal? TransferFeePercent { get; set; }

    /// <summary>FSD field C67: Transfer fee (Baht). Computed: C15×C66.</summary>
    public decimal? TransferFeeAmount { get; set; }

    /// <summary>FSD field C68: Transfer fee govt ratio (%). Computed: (C67×100)/C72.</summary>
    public decimal? TransferFeeRatio { get; set; }

    /// <summary>FSD field C69: Specific business tax (N% of revenue). User input.</summary>
    public decimal? SpecificBizTaxPercent { get; set; }

    /// <summary>FSD field C70: Specific business tax (Baht). Computed: C15×C69.</summary>
    public decimal? SpecificBizTaxAmount { get; set; }

    /// <summary>FSD field C71: Specific biz tax ratio (%). Computed.</summary>
    public decimal? SpecificBizTaxRatio { get; set; }

    /// <summary>FSD field C72: Total govt taxes (Baht). Computed: C67+C70.</summary>
    public decimal? TotalGovTax { get; set; }

    /// <summary>FSD field C73: Total govt tax ratio (%). Computed: 100%.</summary>
    public decimal? TotalGovTaxRatio { get; set; }

    // ── Risk Premium ──────────────────────────────────────────────────────
    /// <summary>FSD field C74: Risk premium (N% of revenue). User input.</summary>
    public decimal? RiskPremiumPercent { get; set; }

    /// <summary>FSD field C75: Risk premium (Baht). Computed: C15×C74.</summary>
    public decimal? RiskPremiumAmount { get; set; }

    // ── Including Dev Costs ────────────────────────────────────────────────
    /// <summary>FSD field C76: Total development costs incl all. Computed: C38+C64+C72+C75.</summary>
    public decimal? TotalDevCostsAndExpenses { get; set; }

    // ── Current Property Value ─────────────────────────────────────────────
    /// <summary>FSD field C77: Current property value.
    /// If C78=0: C77 = C15 - C76.
    /// If C78≠0: C77 = (C15 - C76) × C78 (i.e., C78 is a % applied as a factor).
    /// </summary>
    public decimal? CurrentPropertyValue { get; set; }

    /// <summary>FSD field C78: Discount rate (%). User input. 0 means no discounting.</summary>
    public decimal? DiscountRate { get; set; }

    /// <summary>FSD field C79: Discount rate factor. Computed: 1 / (1 + C78/100)^(C18/12).</summary>
    public decimal? DiscountRateFactor { get; set; }

    /// <summary>FSD field C80: Final property value (Baht). Computed: C77 × C79.</summary>
    public decimal? FinalPropertyValue { get; set; }

    /// <summary>FSD field C81: Total asset value rounded to nearest 10,000.</summary>
    public decimal? TotalAssetValueRounded { get; set; }

    /// <summary>FSD field C82: Total asset value per Sq.Wa, rounded to nearest 100. = C81/C01.</summary>
    public decimal? TotalAssetValuePerSqWa { get; set; }

    public string? Remark { get; set; }

    /// <summary>Deep-clone for CI carry-forward — owned, no FK rewrite needed.</summary>
    internal static LandBuildingSummary Clone(LandBuildingSummary source) => new()
    {
        TotalArea = source.TotalArea,
        SellingAreaPercent = source.SellingAreaPercent,
        SellingArea = source.SellingArea,
        PublicUtilityAreaPercent = source.PublicUtilityAreaPercent,
        PublicUtilityArea = source.PublicUtilityArea,
        TotalRevenue = source.TotalRevenue,
        EstSalesPeriod = source.EstSalesPeriod,
        TotalUnits = source.TotalUnits,
        EstimatedDurationMonths = source.EstimatedDurationMonths,
        PublicUtilityRatePerSqWa = source.PublicUtilityRatePerSqWa,
        PublicUtilityAreaForCost = source.PublicUtilityAreaForCost,
        PublicUtilityCost = source.PublicUtilityCost,
        PublicUtilityCostRatio = source.PublicUtilityCostRatio,
        LandFillingRatePerSqWa = source.LandFillingRatePerSqWa,
        LandFillingArea = source.LandFillingArea,
        LandFillingCost = source.LandFillingCost,
        LandFillingCostRatio = source.LandFillingCostRatio,
        ContingencyPercent = source.ContingencyPercent,
        ContingencyAmount = source.ContingencyAmount,
        ContingencyRatio = source.ContingencyRatio,
        TotalProjectDevCost = source.TotalProjectDevCost,
        TotalDevCostRatio = source.TotalDevCostRatio,
        EstConstructionPeriod = source.EstConstructionPeriod,
        TotalUnitsForConstruction = source.TotalUnitsForConstruction,
        EstimatedConstructionDurationMonths = source.EstimatedConstructionDurationMonths,
        AllocationPermitFee = source.AllocationPermitFee,
        AllocationPermitFeeRatio = source.AllocationPermitFeeRatio,
        LandTitleFeePerPlot = source.LandTitleFeePerPlot,
        TotalPlots = source.TotalPlots,
        LandTitleFeeTotal = source.LandTitleFeeTotal,
        LandTitleFeeRatio = source.LandTitleFeeRatio,
        ProfessionalFeePerMonth = source.ProfessionalFeePerMonth,
        ProfessionalFeeMonths = source.ProfessionalFeeMonths,
        ProfessionalFeeTotal = source.ProfessionalFeeTotal,
        ProfessionalFeeRatio = source.ProfessionalFeeRatio,
        AdminCostPerMonth = source.AdminCostPerMonth,
        AdminCostMonths = source.AdminCostMonths,
        AdminCostTotal = source.AdminCostTotal,
        AdminCostRatio = source.AdminCostRatio,
        SellingAdvPercent = source.SellingAdvPercent,
        SellingAdvTotal = source.SellingAdvTotal,
        SellingAdvRatio = source.SellingAdvRatio,
        ProjectContingencyPercent = source.ProjectContingencyPercent,
        ProjectContingencyAmount = source.ProjectContingencyAmount,
        ProjectContingencyRatio = source.ProjectContingencyRatio,
        TotalProjectCost = source.TotalProjectCost,
        TotalProjectCostRatio = source.TotalProjectCostRatio,
        TransferFeePercent = source.TransferFeePercent,
        TransferFeeAmount = source.TransferFeeAmount,
        TransferFeeRatio = source.TransferFeeRatio,
        SpecificBizTaxPercent = source.SpecificBizTaxPercent,
        SpecificBizTaxAmount = source.SpecificBizTaxAmount,
        SpecificBizTaxRatio = source.SpecificBizTaxRatio,
        TotalGovTax = source.TotalGovTax,
        TotalGovTaxRatio = source.TotalGovTaxRatio,
        RiskPremiumPercent = source.RiskPremiumPercent,
        RiskPremiumAmount = source.RiskPremiumAmount,
        TotalDevCostsAndExpenses = source.TotalDevCostsAndExpenses,
        CurrentPropertyValue = source.CurrentPropertyValue,
        DiscountRate = source.DiscountRate,
        DiscountRateFactor = source.DiscountRateFactor,
        FinalPropertyValue = source.FinalPropertyValue,
        TotalAssetValueRounded = source.TotalAssetValueRounded,
        TotalAssetValuePerSqWa = source.TotalAssetValuePerSqWa,
        Remark = source.Remark
    };
}
