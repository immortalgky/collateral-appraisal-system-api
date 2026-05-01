namespace Appraisal.Domain.Appraisals.Hypothesis.Summaries;

/// <summary>
/// Owned-entity summary block for the Condominium variant.
/// Stores user-input fields and server-computed outputs for E01..E59.
/// Per-unit aggregates from the upload (D01..D04) are reflected in E01/E09/E12.
/// </summary>
public class CondominiumSummary
{
    // ── Land Area Details ──────────────────────────────────────────────────
    /// <summary>E01 — Area according to title deed (Sq.Wa). From unit detail upload.</summary>
    public decimal? E01AreaTitleDeed { get; set; }

    /// <summary>E02 — Area in Square Meters. Computed: E01 × 4.</summary>
    public decimal? E02AreaSqM { get; set; }

    /// <summary>E03 — Floor Area Ratio (FAR). User input (e.g., 40 for 40:1).</summary>
    public decimal? E03FAR { get; set; }

    /// <summary>E04 — Construction area per city plan (SqM). Computed: E02 × E03 (rounded).</summary>
    public decimal? E04ConstructionAreaCityPlan { get; set; }

    /// <summary>E05 — Total building area (SqM). User input.</summary>
    public decimal? E05TotalBuildingArea { get; set; }

    /// <summary>E06 — Common area % (100% - E08). Computed.</summary>
    public decimal? E06CommonAreaPercent { get; set; }

    /// <summary>E07 — Common area (SqM). Computed: E05 - E09.</summary>
    public decimal? E07CommonArea { get; set; }

    /// <summary>E08 — Indoor sales area % of total building area. Computed: E09×100/E05.</summary>
    public decimal? E08IndoorSalesAreaPercent { get; set; }

    /// <summary>E09 — Indoor sales area (SqM). From unit detail upload (D02 total).</summary>
    public decimal? E09IndoorSalesArea { get; set; }

    // ── Project Revenue Estimates ──────────────────────────────────────────
    /// <summary>E10 — Project sales area (SqM). = E09.</summary>
    public decimal? E10ProjectSalesArea { get; set; }

    /// <summary>E11 — Average price per SqM. Computed: E12/E10.</summary>
    public decimal? E11AveragePricePerSqM { get; set; }

    /// <summary>E12 — Total project selling price (Baht). From unit detail upload (D04 total).</summary>
    public decimal? E12TotalProjectSellingPrice { get; set; }

    /// <summary>E13 — Total project revenue (GDV) (Baht). = E12.</summary>
    public decimal? E13TotalRevenue { get; set; }

    // ── Estimate Sales Period ──────────────────────────────────────────────
    /// <summary>E14 — Estimated project sales duration (months). User input.</summary>
    public int? E14EstSalesDurationMonths { get; set; }

    // ── Hard Cost ─────────────────────────────────────────────────────────
    /// <summary>E15 — Condo building construction cost (Baht/SqM). User input.</summary>
    public decimal? E15CondoBuildingCostPerSqM { get; set; }

    /// <summary>E16 — Total building area (SqM). = E05.</summary>
    public decimal? E16BuildingArea { get; set; }

    /// <summary>E17 — Condo building construction cost (Baht). Computed: E15 × E16.</summary>
    public decimal? E17CondoBuildingCostTotal { get; set; }

    /// <summary>E18 — Set average room size (units). = D03.</summary>
    public int? E18SetAvgRoomSizeUnits { get; set; }

    /// <summary>E19 — Average indoor sales area per unit (SqM). Computed: E09/E18.</summary>
    public decimal? E19AvgIndoorSalesAreaPerUnit { get; set; }

    /// <summary>E20 — Furniture/Kitchen/AC per unit (Baht/unit). User input.</summary>
    public decimal? E20FurniturePerUnit { get; set; }

    /// <summary>E21 — Quantity of units. = D03.</summary>
    public int? E21FurnitureQuantity { get; set; }

    /// <summary>E22 — Furniture total (Baht). Computed: E20 × E21.</summary>
    public decimal? E22FurnitureTotal { get; set; }

    /// <summary>E23 — External utilities (Baht). User input.</summary>
    public decimal? E23ExternalUtilities { get; set; }

    /// <summary>E24 — External utilities total (Baht). = E23.</summary>
    public decimal? E24ExternalUtilitiesTotal { get; set; }

    /// <summary>E25 — Contingency (% of building + dev costs). Default 3%.</summary>
    public decimal? E25HardCostContingencyPercent { get; set; }

    /// <summary>E26 — Contingency (Baht). Computed: (E17+E22+E24) × E25/100.</summary>
    public decimal? E26HardCostContingencyAmount { get; set; }

    /// <summary>E27 — Total hard cost (Baht). Computed: E17+E22+E24+E26.</summary>
    public decimal? E27TotalHardCost { get; set; }

    // ── Construction Period ────────────────────────────────────────────────
    /// <summary>E28 — Estimated project construction period (months). User input.</summary>
    public int? E28EstConstructionPeriodMonths { get; set; }

    // ── Soft Cost ─────────────────────────────────────────────────────────
    /// <summary>E29 — Professional service fees (Baht/Month). User input.</summary>
    public decimal? E29ProfessionalFeePerMonth { get; set; }

    /// <summary>E30 — Professional fee months. = E28.</summary>
    public int? E30ProfessionalFeeMonths { get; set; }

    /// <summary>E31 — Professional fee total (Baht). Computed: E29 × E30.</summary>
    public decimal? E31ProfessionalFeeTotal { get; set; }

    /// <summary>E32 — Project admin cost (Baht/Month). User input.</summary>
    public decimal? E32AdminCostPerMonth { get; set; }

    /// <summary>E33 — Admin cost months. = E14.</summary>
    public int? E33AdminCostMonths { get; set; }

    /// <summary>E34 — Admin cost total (Baht). Computed: E32 × E33.</summary>
    public decimal? E34AdminCostTotal { get; set; }

    /// <summary>E35 — Selling/Adv expenses (N% of project income). User input.</summary>
    public decimal? E35SellingAdvPercent { get; set; }

    /// <summary>E36 — Selling/Adv total (Baht). Computed: E13 × E35.</summary>
    public decimal? E36SellingAdvTotal { get; set; }

    /// <summary>E37 — Condo title deed issuance fee (Baht). User input.</summary>
    public decimal? E37TitleDeedFee { get; set; }

    /// <summary>E38 — Condo title deed fee total (Baht). = E37.</summary>
    public decimal? E38TitleDeedFeeTotal { get; set; }

    /// <summary>E39 — EIA report cost (Baht). User input.</summary>
    public decimal? E39EIACost { get; set; }

    /// <summary>E40 — EIA report cost total (Baht). = E39.</summary>
    public decimal? E40EIACostTotal { get; set; }

    /// <summary>E41 — Condo registration permit fee (Baht). User input.</summary>
    public decimal? E41CondoRegistrationFee { get; set; }

    /// <summary>E42 — Condo registration fee total (Baht). = E41.</summary>
    public decimal? E42CondoRegistrationFeeTotal { get; set; }

    /// <summary>E43 — Other expenses (N% of project cost expenses). User input.</summary>
    public decimal? E43OtherExpensesPercent { get; set; }

    /// <summary>E44 — Other expenses total (Baht). Computed: (E31+E34+E36+E38+E40+E42) × E43/100.</summary>
    public decimal? E44OtherExpensesTotal { get; set; }

    /// <summary>E45 — Total soft cost (Baht). Computed: E31+E34+E36+E38+E40+E42+E44.</summary>
    public decimal? E45TotalSoftCost { get; set; }

    // ── Government Taxes ──────────────────────────────────────────────────
    /// <summary>E46 — Transfer fee (N% of project income). Default 1%. User input.</summary>
    public decimal? E46TransferFeePercent { get; set; }

    /// <summary>E47 — Transfer fee total (Baht). Computed: E13 × E46.</summary>
    public decimal? E47TransferFeeTotal { get; set; }

    /// <summary>E48 — Specific business tax (N% of project income). User input.</summary>
    public decimal? E48SpecificBizTaxPercent { get; set; }

    /// <summary>E49 — Specific business tax total (Baht). Computed: E13 × E48.</summary>
    public decimal? E49SpecificBizTaxTotal { get; set; }

    /// <summary>E50 — Total govt taxes (Baht). Computed: E47+E49.</summary>
    public decimal? E50TotalGovTax { get; set; }

    // ── Risk Premium ──────────────────────────────────────────────────────
    /// <summary>E51 — Risk and expected profit (N% of project income). User input.</summary>
    public decimal? E51RiskProfitPercent { get; set; }

    /// <summary>E52 — Risk and expected profit total (Baht). Computed: E13 × E51.</summary>
    public decimal? E52RiskProfitTotal { get; set; }

    // ── Total Dev Costs ────────────────────────────────────────────────────
    /// <summary>E53 — Total dev costs (Baht). Computed: E27+E45+E50+E52.</summary>
    public decimal? E53TotalDevCosts { get; set; }

    // ── Final Value ───────────────────────────────────────────────────────
    /// <summary>E54 — Remaining value (Baht).
    /// If E55=0: E54 = E13 - E53.
    /// If E55≠0: E54 = (E13 - E53) × E55 (% factor interpretation per FSD).
    /// </summary>
    public decimal? E54TotalRemainingValue { get; set; }

    /// <summary>E55 — Discount rate (%). User input. 0 = no discounting.</summary>
    public decimal? E55DiscountRate { get; set; }

    /// <summary>E56 — Discount rate factor. Computed: 1 / (1 + E55/100)^(E14/12).</summary>
    public decimal? E56DiscountRateFactor { get; set; }

    /// <summary>E57 — Final remaining value (Baht). Computed: E54 × E56.</summary>
    public decimal? E57FinalRemainingValue { get; set; }

    /// <summary>E58 — Total asset value rounded to nearest 10,000.</summary>
    public decimal? E58TotalAssetValueRounded { get; set; }

    /// <summary>E59 — Total asset value per SqM, rounded to nearest 100. = E58/E05.</summary>
    public decimal? E59TotalAssetValuePerSqM { get; set; }

    public string? Remark { get; set; }
}
