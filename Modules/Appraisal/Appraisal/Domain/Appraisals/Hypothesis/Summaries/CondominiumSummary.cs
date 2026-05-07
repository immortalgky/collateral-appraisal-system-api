namespace Appraisal.Domain.Appraisals.Hypothesis.Summaries;

/// <summary>
/// Owned-entity summary block for the Condominium variant.
/// Stores user-input fields and server-computed outputs for E01..E59.
/// Per-unit aggregates from the upload (D01..D04) are reflected in E01/E09/E12.
/// </summary>
public class CondominiumSummary
{
    // ── Land Area Details ──────────────────────────────────────────────────
    /// <summary>FSD field E01: Area according to title deed (Sq.Wa). From unit detail upload.</summary>
    public decimal? AreaTitleDeed { get; set; }

    /// <summary>FSD field E02: Area in Square Meters. Computed: E01 × 4.</summary>
    public decimal? AreaSqM { get; set; }

    /// <summary>FSD field E03: Floor Area Ratio (FAR). User input (e.g., 40 for 40:1).</summary>
    public decimal? FAR { get; set; }

    /// <summary>FSD field E04: Construction area per city plan (SqM). Computed: E02 × E03 (rounded).</summary>
    public decimal? ConstructionAreaCityPlan { get; set; }

    /// <summary>FSD field E05: Total building area (SqM). User input.</summary>
    public decimal? TotalBuildingArea { get; set; }

    /// <summary>FSD field E06: Common area % (100% - E08). Computed.</summary>
    public decimal? CommonAreaPercent { get; set; }

    /// <summary>FSD field E07: Common area (SqM). Computed: E05 - E09.</summary>
    public decimal? CommonArea { get; set; }

    /// <summary>FSD field E08: Indoor sales area % of total building area. Computed: E09×100/E05.</summary>
    public decimal? IndoorSalesAreaPercent { get; set; }

    /// <summary>FSD field E09: Indoor sales area (SqM). From unit detail upload (D02 total).</summary>
    public decimal? IndoorSalesArea { get; set; }

    // ── Project Revenue Estimates ──────────────────────────────────────────
    /// <summary>FSD field E10: Project sales area (SqM). = E09.</summary>
    public decimal? ProjectSalesArea { get; set; }

    /// <summary>FSD field E11: Average price per SqM. Computed: E12/E10.</summary>
    public decimal? AveragePricePerSqM { get; set; }

    /// <summary>FSD field E12: Total project selling price (Baht). From unit detail upload (D04 total).</summary>
    public decimal? TotalProjectSellingPrice { get; set; }

    /// <summary>FSD field E13: Total project revenue (GDV) (Baht). = E12.</summary>
    public decimal? TotalRevenue { get; set; }

    // ── Estimate Sales Period ──────────────────────────────────────────────
    /// <summary>FSD field E14: Estimated project sales duration (months). User input.</summary>
    public int? EstSalesDurationMonths { get; set; }

    // ── Hard Cost ─────────────────────────────────────────────────────────
    /// <summary>FSD field E15: Condo building construction cost (Baht/SqM). User input.</summary>
    public decimal? CondoBuildingCostPerSqM { get; set; }

    /// <summary>FSD field E16: Total building area (SqM). = E05.</summary>
    public decimal? BuildingArea { get; set; }

    /// <summary>FSD field E17: Condo building construction cost (Baht). Computed: E15 × E16.</summary>
    public decimal? CondoBuildingCostTotal { get; set; }

    /// <summary>FSD field E18: Set average room size (units). = D03.</summary>
    public int? SetAvgRoomSizeUnits { get; set; }

    /// <summary>FSD field E19: Average indoor sales area per unit (SqM). Computed: E09/E18.</summary>
    public decimal? AvgIndoorSalesAreaPerUnit { get; set; }

    /// <summary>FSD field E20: Furniture/Kitchen/AC per unit (Baht/unit). User input.</summary>
    public decimal? FurniturePerUnit { get; set; }

    /// <summary>FSD field E21: Quantity of units. = D03.</summary>
    public int? FurnitureQuantity { get; set; }

    /// <summary>FSD field E22: Furniture total (Baht). Computed: E20 × E21.</summary>
    public decimal? FurnitureTotal { get; set; }

    /// <summary>FSD field E23: External utilities (Baht). User input.</summary>
    public decimal? ExternalUtilities { get; set; }

    /// <summary>FSD field E24: External utilities total (Baht). = E23.</summary>
    public decimal? ExternalUtilitiesTotal { get; set; }

    /// <summary>FSD field E25: Contingency (% of building + dev costs). Default 3%.</summary>
    public decimal? HardCostContingencyPercent { get; set; }

    /// <summary>FSD field E26: Contingency (Baht). Computed: (E17+E22+E24) × E25/100.</summary>
    public decimal? HardCostContingencyAmount { get; set; }

    /// <summary>FSD field E27: Total hard cost (Baht). Computed: E17+E22+E24+E26.</summary>
    public decimal? TotalHardCost { get; set; }

    // ── Construction Period ────────────────────────────────────────────────
    /// <summary>FSD field E28: Estimated project construction period (months). User input.</summary>
    public int? EstConstructionPeriodMonths { get; set; }

    // ── Soft Cost ─────────────────────────────────────────────────────────
    /// <summary>FSD field E29: Professional service fees (Baht/Month). User input.</summary>
    public decimal? ProfessionalFeePerMonth { get; set; }

    /// <summary>FSD field E30: Professional fee months. = E28.</summary>
    public int? ProfessionalFeeMonths { get; set; }

    /// <summary>FSD field E31: Professional fee total (Baht). Computed: E29 × E30.</summary>
    public decimal? ProfessionalFeeTotal { get; set; }

    /// <summary>FSD field E32: Project admin cost (Baht/Month). User input.</summary>
    public decimal? AdminCostPerMonth { get; set; }

    /// <summary>FSD field E33: Admin cost months. = E14.</summary>
    public int? AdminCostMonths { get; set; }

    /// <summary>FSD field E34: Admin cost total (Baht). Computed: E32 × E33.</summary>
    public decimal? AdminCostTotal { get; set; }

    /// <summary>FSD field E35: Selling/Adv expenses (N% of project income). User input.</summary>
    public decimal? SellingAdvPercent { get; set; }

    /// <summary>FSD field E36: Selling/Adv total (Baht). Computed: E13 × E35.</summary>
    public decimal? SellingAdvTotal { get; set; }

    /// <summary>FSD field E37: Condo title deed issuance fee (Baht). User input.</summary>
    public decimal? TitleDeedFee { get; set; }

    /// <summary>FSD field E38: Condo title deed fee total (Baht). = E37.</summary>
    public decimal? TitleDeedFeeTotal { get; set; }

    /// <summary>FSD field E39: EIA report cost (Baht). User input.</summary>
    public decimal? EIACost { get; set; }

    /// <summary>FSD field E40: EIA report cost total (Baht). = E39.</summary>
    public decimal? EIACostTotal { get; set; }

    /// <summary>FSD field E41: Condo registration permit fee (Baht). User input.</summary>
    public decimal? CondoRegistrationFee { get; set; }

    /// <summary>FSD field E42: Condo registration fee total (Baht). = E41.</summary>
    public decimal? CondoRegistrationFeeTotal { get; set; }

    /// <summary>FSD field E43: Other expenses (N% of project cost expenses). User input.</summary>
    public decimal? OtherExpensesPercent { get; set; }

    /// <summary>FSD field E44: Other expenses total (Baht). Computed: (E31+E34+E36+E38+E40+E42) × E43/100.</summary>
    public decimal? OtherExpensesTotal { get; set; }

    /// <summary>FSD field E45: Total soft cost (Baht). Computed: E31+E34+E36+E38+E40+E42+E44.</summary>
    public decimal? TotalSoftCost { get; set; }

    // ── Government Taxes ──────────────────────────────────────────────────
    /// <summary>FSD field E46: Transfer fee (N% of project income). Default 1%. User input.</summary>
    public decimal? TransferFeePercent { get; set; }

    /// <summary>FSD field E47: Transfer fee total (Baht). Computed: E13 × E46.</summary>
    public decimal? TransferFeeTotal { get; set; }

    /// <summary>FSD field E48: Specific business tax (N% of project income). User input.</summary>
    public decimal? SpecificBizTaxPercent { get; set; }

    /// <summary>FSD field E49: Specific business tax total (Baht). Computed: E13 × E48.</summary>
    public decimal? SpecificBizTaxTotal { get; set; }

    /// <summary>FSD field E50: Total govt taxes (Baht). Computed: E47+E49.</summary>
    public decimal? TotalGovTax { get; set; }

    // ── Risk Premium ──────────────────────────────────────────────────────
    /// <summary>FSD field E51: Risk and expected profit (N% of project income). User input.</summary>
    public decimal? RiskProfitPercent { get; set; }

    /// <summary>FSD field E52: Risk and expected profit total (Baht). Computed: E13 × E51.</summary>
    public decimal? RiskProfitTotal { get; set; }

    // ── Total Dev Costs ────────────────────────────────────────────────────
    /// <summary>FSD field E53: Total dev costs (Baht). Computed: E27+E45+E50+E52.</summary>
    public decimal? TotalDevCosts { get; set; }

    // ── Final Value ───────────────────────────────────────────────────────
    /// <summary>FSD field E54: Remaining value (Baht).
    /// If E55=0: E54 = E13 - E53.
    /// If E55≠0: E54 = (E13 - E53) × E55 (% factor interpretation per FSD).
    /// </summary>
    public decimal? TotalRemainingValue { get; set; }

    /// <summary>FSD field E55: Discount rate (%). User input. 0 = no discounting.</summary>
    public decimal? DiscountRate { get; set; }

    /// <summary>FSD field E56: Discount rate factor. Computed: 1 / (1 + E55/100)^(E14/12).</summary>
    public decimal? DiscountRateFactor { get; set; }

    /// <summary>FSD field E57: Final remaining value (Baht). Computed: E54 × E56.</summary>
    public decimal? FinalRemainingValue { get; set; }

    /// <summary>FSD field E58: Total asset value rounded to nearest 10,000.</summary>
    public decimal? TotalAssetValueRounded { get; set; }

    /// <summary>FSD field E59: Total asset value per SqM, rounded to nearest 100. = E58/E05.</summary>
    public decimal? TotalAssetValuePerSqM { get; set; }

    public string? Remark { get; set; }

    /// <summary>Deep-clone for CI carry-forward — owned, no FK rewrite needed.</summary>
    internal static CondominiumSummary Clone(CondominiumSummary source) => new()
    {
        AreaTitleDeed = source.AreaTitleDeed,
        AreaSqM = source.AreaSqM,
        FAR = source.FAR,
        ConstructionAreaCityPlan = source.ConstructionAreaCityPlan,
        TotalBuildingArea = source.TotalBuildingArea,
        CommonAreaPercent = source.CommonAreaPercent,
        CommonArea = source.CommonArea,
        IndoorSalesAreaPercent = source.IndoorSalesAreaPercent,
        IndoorSalesArea = source.IndoorSalesArea,
        ProjectSalesArea = source.ProjectSalesArea,
        AveragePricePerSqM = source.AveragePricePerSqM,
        TotalProjectSellingPrice = source.TotalProjectSellingPrice,
        TotalRevenue = source.TotalRevenue,
        EstSalesDurationMonths = source.EstSalesDurationMonths,
        CondoBuildingCostPerSqM = source.CondoBuildingCostPerSqM,
        BuildingArea = source.BuildingArea,
        CondoBuildingCostTotal = source.CondoBuildingCostTotal,
        SetAvgRoomSizeUnits = source.SetAvgRoomSizeUnits,
        AvgIndoorSalesAreaPerUnit = source.AvgIndoorSalesAreaPerUnit,
        FurniturePerUnit = source.FurniturePerUnit,
        FurnitureQuantity = source.FurnitureQuantity,
        FurnitureTotal = source.FurnitureTotal,
        ExternalUtilities = source.ExternalUtilities,
        ExternalUtilitiesTotal = source.ExternalUtilitiesTotal,
        HardCostContingencyPercent = source.HardCostContingencyPercent,
        HardCostContingencyAmount = source.HardCostContingencyAmount,
        TotalHardCost = source.TotalHardCost,
        EstConstructionPeriodMonths = source.EstConstructionPeriodMonths,
        ProfessionalFeePerMonth = source.ProfessionalFeePerMonth,
        ProfessionalFeeMonths = source.ProfessionalFeeMonths,
        ProfessionalFeeTotal = source.ProfessionalFeeTotal,
        AdminCostPerMonth = source.AdminCostPerMonth,
        AdminCostMonths = source.AdminCostMonths,
        AdminCostTotal = source.AdminCostTotal,
        SellingAdvPercent = source.SellingAdvPercent,
        SellingAdvTotal = source.SellingAdvTotal,
        TitleDeedFee = source.TitleDeedFee,
        TitleDeedFeeTotal = source.TitleDeedFeeTotal,
        EIACost = source.EIACost,
        EIACostTotal = source.EIACostTotal,
        CondoRegistrationFee = source.CondoRegistrationFee,
        CondoRegistrationFeeTotal = source.CondoRegistrationFeeTotal,
        OtherExpensesPercent = source.OtherExpensesPercent,
        OtherExpensesTotal = source.OtherExpensesTotal,
        TotalSoftCost = source.TotalSoftCost,
        TransferFeePercent = source.TransferFeePercent,
        TransferFeeTotal = source.TransferFeeTotal,
        SpecificBizTaxPercent = source.SpecificBizTaxPercent,
        SpecificBizTaxTotal = source.SpecificBizTaxTotal,
        TotalGovTax = source.TotalGovTax,
        RiskProfitPercent = source.RiskProfitPercent,
        RiskProfitTotal = source.RiskProfitTotal,
        TotalDevCosts = source.TotalDevCosts,
        TotalRemainingValue = source.TotalRemainingValue,
        DiscountRate = source.DiscountRate,
        DiscountRateFactor = source.DiscountRateFactor,
        FinalRemainingValue = source.FinalRemainingValue,
        TotalAssetValueRounded = source.TotalAssetValueRounded,
        TotalAssetValuePerSqM = source.TotalAssetValuePerSqM,
        Remark = source.Remark
    };
}
