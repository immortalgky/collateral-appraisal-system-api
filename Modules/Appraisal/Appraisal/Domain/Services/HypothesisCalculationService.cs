using Appraisal.Domain.Appraisals.Hypothesis;
using Appraisal.Domain.Appraisals.Hypothesis.CostItems;
using Appraisal.Domain.Appraisals.Hypothesis.Summaries;
using Appraisal.Domain.Appraisals.Hypothesis.Uploads;

namespace Appraisal.Domain.Services;

/// <summary>
/// Stateless server-side calculation service for the Hypothesis / Residual pricing method.
/// Implements FSD §2.1.3.7 formulas for both variants.
///
/// Rounding rules (FSD):
///   C81 = Round(C80, -4)  — nearest 10,000
///   C82 = Round(C80 / C01, -2) — nearest 100
///   E58 = Round(E57, -4)
///   E59 = Round(E57 / E05, -2)
///
/// Discount rate factor (Reading 2 — literal FSD precedence):
///   C79 = 1 / (1 + (C78/100)^(C18/12))
///   E56 = 1 / (1 + (E55/100)^(E14/12))
///
/// C77 conditional (percentage applied when C78 ≠ 0):
///   if C78 = 0: C77 = C15 - C76
///   if C78 ≠ 0: C77 = (C15 – C76) × (C78 / 100)
///
/// E54 conditional (mirrors C77):
///   if E55 = 0: E54 = E13 – E53
///   if E55 ≠ 0: E54 = (E13 – E53) × (E55 / 100)
/// </summary>
public class HypothesisCalculationService
{
    // ── L&B public API ────────────────────────────────────────────────────

    /// <summary>
    /// Computes all L&amp;B C-field formulas given the analysis and the per-model cost items.
    /// Populates and returns an updated <see cref="LandBuildingSummary"/>.
    /// The per-model detail snapshot (A-fields, per-model C11-C26) is included in the result.
    /// </summary>
    public LandBuildingSnapshot ComputeLandBuilding(
        HypothesisAnalysis analysis,
        IReadOnlyList<LandBuildingUnitRow> rows,
        LandBuildingSummary input)
    {
        return ComputeLandBuildingCore(analysis.CostItems, rows, input);
    }

    /// <summary>
    /// Overload that accepts a pre-resolved cost-item list directly.
    /// Used by the preview handler to avoid building a throwaway aggregate.
    /// </summary>
    public LandBuildingSnapshot ComputeLandBuilding(
        IReadOnlyList<HypothesisCostItem> costItems,
        IReadOnlyList<LandBuildingUnitRow> rows,
        LandBuildingSummary input)
    {
        return ComputeLandBuildingCore(costItems, rows, input);
    }

    private static LandBuildingSnapshot ComputeLandBuildingCore(
        IReadOnlyList<HypothesisCostItem> costItems,
        IReadOnlyList<LandBuildingUnitRow> rows,
        LandBuildingSummary input)
    {
        // ── Step 1: Aggregate per-model A/C fields from unit rows ─────────
        var models = AggregateModels(rows);

        // ── Step 2: Area calculations (C01-C10A) ──────────────────────────
        // FSD C01: Total area
        decimal c01 = input.TotalArea ?? 0m;
        // FSD C02: Selling area percent
        decimal c02 = input.SellingAreaPercent ?? 0m;
        // FSD C03: Selling area = SUM of per-model total land areas
        decimal c03 = models.Values.Sum(m => m.TotalLandAreaSqWa);
        // FSD C10: Public utility area percent
        decimal c10 = input.PublicUtilityAreaPercent ?? 0m;
        // FSD C10A: Public utility area
        decimal c10a = c10 / 100m * c01;

        // ── Step 3: Revenue (C11-C15) ─────────────────────────────────────
        // Per-model: C11 = units, C12 = total selling price (summed from rows)
        // FSD C15: Total project revenue
        decimal c15 = models.Values.Sum(m => m.TotalSellingPrice);
        // FSD C17: Total units
        int c17 = models.Values.Sum(m => m.UnitCount);
        // FSD C16: Estimated sales period
        int c16 = input.EstSalesPeriod ?? 1;
        // FSD C18: Estimated duration months
        int c18 = c16 > 0 ? (int)Math.Ceiling((double)c17 / c16) : 0;

        // ── Step 4: Per-model construction cost from cost items ───────────
        // FSD C21+C25+...: sum building cost all models
        decimal sumBuildingCostAllModels = 0m;

        foreach (var model in models.Values)
        {
            // FSD C19: total value after depreciation for this model (from cost-of-building items)
            decimal c19 = costItems
                .Where(i => i.Category == HypothesisCostCategory.CostOfBuilding
                             && i.ModelName == model.ModelName)
                .Sum(i => i.Amount);
            model.TotalValueAfterDepreciation = c19;
            // FSD C21: C19 × C20 (C20 = unit count for the model)
            model.TotalValueAfterDepreciationAllUnits = c19 * model.UnitCount;
            sumBuildingCostAllModels += model.TotalValueAfterDepreciationAllUnits;
        }

        // ── Step 5: Project Dev Cost (C27-C39) ────────────────────────────
        // FSD C27: Public utility rate per SqWa
        decimal c27 = input.PublicUtilityRatePerSqWa ?? 0m;
        // FSD C28: Public utility area = C01
        decimal c28 = c01;
        // FSD C29: Public utility cost
        decimal c29 = c27 * c28;

        // FSD C31: Land filling rate per SqWa
        decimal c31 = input.LandFillingRatePerSqWa ?? 0m;
        // FSD C32: Land filling area = C01
        decimal c32 = c01;
        // FSD C33: Land filling cost
        decimal c33 = c31 * c32;

        // FSD C35: Contingency percent
        decimal c35 = input.ContingencyPercent ?? 3m;
        // FSD C36: Contingency = (sum of building costs + public utility + land filling) × C35 / 100
        decimal c36 = (sumBuildingCostAllModels + c29 + c33) * c35 / 100m;

        // FSD C38: Total project dev cost
        decimal c38 = sumBuildingCostAllModels + c29 + c33 + c36;
        // FSD C39: Total dev cost ratio
        decimal c39 = c38 > 0 ? 100m : 0m;

        // Ratios
        decimal c30 = c38 > 0 ? c29 * 100m / c38 : 0m;
        decimal c34 = c38 > 0 ? c33 * 100m / c38 : 0m;
        decimal c37 = c38 > 0 ? c36 * 100m / c38 : 0m;

        // Per-model dev cost ratios (C22, C26, ...)
        foreach (var model in models.Values)
        {
            model.DevCostRatioPercent = c38 > 0
                ? model.TotalValueAfterDepreciationAllUnits * 100m / c38
                : 0m;
        }

        // ── Step 6: Construction period (C40-C42) ─────────────────────────
        // FSD C40: Estimated construction period
        int c40 = input.EstConstructionPeriod ?? 1;
        // FSD C41: Total units = C17
        int c41 = c17;
        // FSD C42: Estimated construction duration months
        int c42 = c40 > 0 ? (int)Math.Ceiling((double)c41 / c40) : 0;

        // ── Step 7: Project Cost (C43-C65) ────────────────────────────────
        // FSD C44: Allocation permit fee — read from ProjectCost cost item by Kind
        decimal c43 = GetProjectCostAmount(costItems, HypothesisCostCategory.ProjectCost, CostItemKind.AllocationPermitFee);
        decimal c44 = c43; // = C43

        // FSD C46: Land title deed division fee per plot
        decimal c46 = input.LandTitleFeePerPlot ?? 0m;
        // FSD C47: Total plots = C41
        int c47 = c41;
        // FSD C48: Land title deed division fee total
        decimal c48 = c46 * c47;

        // FSD C50: Professional fee per month
        decimal c50 = input.ProfessionalFeePerMonth ?? 0m;
        // FSD C51: Professional fee months = C42
        int c51 = c42;
        // FSD C52: Professional fee total
        decimal c52 = c50 * c51;

        // FSD C54: Admin cost per month
        decimal c54 = input.AdminCostPerMonth ?? 0m;
        // FSD C55: Admin cost months = C18
        int c55 = c18;
        // FSD C56: Admin cost total
        decimal c56 = c54 * c55;

        // FSD C58: Selling/adv percent
        decimal c58 = input.SellingAdvPercent ?? 0m;
        // FSD C59: Selling/adv total
        decimal c59 = c15 * c58 / 100m;

        // FSD C61: Project contingency percent
        decimal c61 = input.ProjectContingencyPercent ?? 3m;
        // FSD C62: Project contingency amount
        decimal c62 = (c44 + c48 + c52 + c56 + c59) * c61 / 100m;

        // FSD C64: Total project cost
        decimal c64 = c44 + c48 + c52 + c56 + c59 + c62;
        // FSD C65: Total project cost ratio
        decimal c65 = c64 > 0 ? 100m : 0m;

        // Ratios
        decimal c45 = c64 > 0 ? c44 * 100m / c64 : 0m;
        decimal c49 = c64 > 0 ? c48 * 100m / c64 : 0m;
        decimal c53 = c64 > 0 ? c52 * 100m / c64 : 0m;
        decimal c57 = c64 > 0 ? c56 * 100m / c64 : 0m;
        decimal c60 = c64 > 0 ? c59 * 100m / c64 : 0m;
        decimal c63 = c64 > 0 ? c62 * 100m / c64 : 0m;

        // ── Step 8: Government Taxes (C66-C73) ────────────────────────────
        // FSD C66: Transfer fee percent
        decimal c66 = input.TransferFeePercent ?? 0m;
        // FSD C67: Transfer fee amount
        decimal c67 = c15 * c66 / 100m;

        // FSD C69: Specific biz tax percent
        decimal c69 = input.SpecificBizTaxPercent ?? 0m;
        // FSD C70: Specific biz tax amount
        decimal c70 = c15 * c69 / 100m;

        // FSD C72: Total gov tax
        decimal c72 = c67 + c70;
        // FSD C73: Total gov tax ratio
        decimal c73 = c72 > 0 ? 100m : 0m;
        decimal c68 = c72 > 0 ? c67 * 100m / c72 : 0m;
        decimal c71 = c72 > 0 ? c70 * 100m / c72 : 0m;

        // ── Step 9: Risk Premium (C74-C75) ────────────────────────────────
        // FSD C74: Risk premium percent
        decimal c74 = input.RiskPremiumPercent ?? 0m;
        // FSD C75: Risk premium amount
        decimal c75 = c15 * c74 / 100m;

        // ── Step 10: Total dev costs (C76) ────────────────────────────────
        // FSD C76: Total dev costs and expenses
        decimal c76 = c38 + c64 + c72 + c75;

        // ── Step 11: Current property value (C77-C82) ─────────────────────
        // FSD C78: Discount rate
        decimal c78 = input.DiscountRate ?? 0m;

        // FSD C77: Current Property Value
        // if C78=0 → residual unchanged; else apply C78 as a percentage factor.
        decimal c77;
        if (c78 == 0m)
            c77 = c15 - c76;
        else
            c77 = (c15 - c76) * (c78 / 100m);

        // FSD C79: Discount rate factor (Reading 2 — literal FSD precedence):
        //   C79 = 1 / (1 + (C78/100)^(C18/12))
        decimal c79;
        if (c78 == 0m)
        {
            c79 = 1m;
        }
        else
        {
            var innerPow = (double)(c78 / 100m);
            var exponent = (double)c18 / 12.0;
            c79 = 1m / (1m + (decimal)Math.Pow(innerPow, exponent));
        }

        // FSD C80: Final property value
        decimal c80 = c77 * c79;
        // FSD C81: Total asset value rounded to nearest 10,000
        decimal c81 = RoundToNearest(c80, 10000m);
        // FSD C82: Total asset value per SqWa rounded to nearest 100
        decimal c82 = c01 > 0m ? RoundToNearest(c81 / c01, 100m) : 0m;

        // ── Build the updated summary ─────────────────────────────────────
        var summary = new LandBuildingSummary
        {
            TotalArea = c01,                         // FSD C01
            SellingAreaPercent = c02,                // FSD C02
            SellingArea = c03,                       // FSD C03
            PublicUtilityAreaPercent = c10,          // FSD C10
            PublicUtilityArea = c10a,                // FSD C10A
            TotalRevenue = c15,                      // FSD C15
            EstSalesPeriod = c16,                    // FSD C16
            TotalUnits = c17,                        // FSD C17
            EstimatedDurationMonths = c18,           // FSD C18
            PublicUtilityRatePerSqWa = c27,          // FSD C27
            PublicUtilityAreaForCost = c28,          // FSD C28
            PublicUtilityCost = c29,                 // FSD C29
            PublicUtilityCostRatio = c30,            // FSD C30
            LandFillingRatePerSqWa = c31,            // FSD C31
            LandFillingArea = c32,                   // FSD C32
            LandFillingCost = c33,                   // FSD C33
            LandFillingCostRatio = c34,              // FSD C34
            ContingencyPercent = c35,                // FSD C35
            ContingencyAmount = c36,                 // FSD C36
            ContingencyRatio = c37,                  // FSD C37
            TotalProjectDevCost = c38,               // FSD C38
            TotalDevCostRatio = c39,                 // FSD C39
            EstConstructionPeriod = c40,             // FSD C40
            TotalUnitsForConstruction = c41,         // FSD C41
            EstimatedConstructionDurationMonths = c42, // FSD C42
            AllocationPermitFee = c44,               // FSD C44
            AllocationPermitFeeRatio = c45,          // FSD C45
            LandTitleFeePerPlot = c46,               // FSD C46
            TotalPlots = c47,                        // FSD C47
            LandTitleFeeTotal = c48,                 // FSD C48
            LandTitleFeeRatio = c49,                 // FSD C49
            ProfessionalFeePerMonth = c50,           // FSD C50
            ProfessionalFeeMonths = c51,             // FSD C51
            ProfessionalFeeTotal = c52,              // FSD C52
            ProfessionalFeeRatio = c53,              // FSD C53
            AdminCostPerMonth = c54,                 // FSD C54
            AdminCostMonths = c55,                   // FSD C55
            AdminCostTotal = c56,                    // FSD C56
            AdminCostRatio = c57,                    // FSD C57
            SellingAdvPercent = c58,                 // FSD C58
            SellingAdvTotal = c59,                   // FSD C59
            SellingAdvRatio = c60,                   // FSD C60
            ProjectContingencyPercent = c61,         // FSD C61
            ProjectContingencyAmount = c62,          // FSD C62
            ProjectContingencyRatio = c63,           // FSD C63
            TotalProjectCost = c64,                  // FSD C64
            TotalProjectCostRatio = c65,             // FSD C65
            TransferFeePercent = c66,                // FSD C66
            TransferFeeAmount = c67,                 // FSD C67
            TransferFeeRatio = c68,                  // FSD C68
            SpecificBizTaxPercent = c69,             // FSD C69
            SpecificBizTaxAmount = c70,              // FSD C70
            SpecificBizTaxRatio = c71,               // FSD C71
            TotalGovTax = c72,                       // FSD C72
            TotalGovTaxRatio = c73,                  // FSD C73
            RiskPremiumPercent = c74,                // FSD C74
            RiskPremiumAmount = c75,                 // FSD C75
            TotalDevCostsAndExpenses = c76,          // FSD C76
            CurrentPropertyValue = c77,              // FSD C77
            DiscountRate = c78,                      // FSD C78
            DiscountRateFactor = c79,                // FSD C79
            FinalPropertyValue = c80,                // FSD C80
            TotalAssetValueRounded = c81,            // FSD C81
            TotalAssetValuePerSqWa = c82,            // FSD C82
            Remark = input.Remark
        };

        return new LandBuildingSnapshot(summary, models);
    }

    // ── Condo public API ──────────────────────────────────────────────────

    /// <summary>
    /// Computes all Condo E-field formulas given the analysis and unit rows.
    /// Populates and returns an updated <see cref="CondominiumSummary"/>.
    /// </summary>
    public CondominiumSummary ComputeCondominium(
        HypothesisAnalysis analysis,
        IReadOnlyList<CondominiumUnitRow> rows,
        CondominiumSummary input)
    {
        return ComputeCondominiumCore(analysis.CostItems, rows, input);
    }

    /// <summary>
    /// Overload that accepts a pre-resolved cost-item list directly.
    /// Used by the preview handler to avoid building a throwaway aggregate.
    /// </summary>
    public CondominiumSummary ComputeCondominium(
        IReadOnlyList<HypothesisCostItem> costItems,
        IReadOnlyList<CondominiumUnitRow> rows,
        CondominiumSummary input)
    {
        return ComputeCondominiumCore(costItems, rows, input);
    }

    private static CondominiumSummary ComputeCondominiumCore(
        IReadOnlyList<HypothesisCostItem> costItems,
        IReadOnlyList<CondominiumUnitRow> rows,
        CondominiumSummary input)
    {
        // ── Step 1: Aggregate from upload (D01-D04 → E01/E09/E12/E18) ─────
        // FSD D02: total indoor sales area
        decimal d02 = rows.Sum(r => r.UsableAreaSqM ?? 0m);
        // FSD D03: total units
        int d03 = rows.Count;
        // FSD D04: total selling price
        decimal d04 = rows.Sum(r => r.SellingPrice ?? 0m);

        // ── Step 2: Land area details ─────────────────────────────────────
        // FSD E01: Area title deed
        decimal e01 = input.AreaTitleDeed ?? 0m;
        // FSD E02: Area SqM = E01 × 4
        decimal e02 = e01 * 4m;
        // FSD E03: FAR
        decimal e03 = input.FAR ?? 0m;
        // FSD E04: Construction area city plan
        decimal e04 = e03 > 0m ? Math.Round(e02 * e03, 0, MidpointRounding.AwayFromZero) : 0m;
        // FSD E05: Total building area
        decimal e05 = input.TotalBuildingArea ?? 0m;

        // FSD E09: Indoor sales area from upload
        decimal e09 = d02;
        // FSD E08: Indoor sales area percent
        decimal e08 = e05 > 0m ? e09 * 100m / e05 : 0m;
        // FSD E06: Common area percent
        decimal e06 = 100m - e08;
        // FSD E07: Common area
        decimal e07 = e05 - e09;

        // ── Step 3: Revenue (E10-E13) ─────────────────────────────────────
        // FSD E10: Project sales area = E09
        decimal e10 = e09;
        // FSD E12: Total project selling price from upload
        decimal e12 = d04;
        // FSD E11: Average price per SqM
        decimal e11 = e10 > 0m ? e12 / e10 : 0m;
        // FSD E13: Total revenue = E12
        decimal e13 = e12;

        // FSD E14: Estimated sales duration months
        int e14 = input.EstSalesDurationMonths ?? 0;

        // ── Step 4: Hard Cost (E15-E27) ───────────────────────────────────
        // FSD E15: Condo building cost per SqM
        decimal e15 = input.CondoBuildingCostPerSqM ?? 0m;
        // FSD E16: Building area = E05
        decimal e16 = e05;
        // FSD E17: Condo building cost total
        decimal e17 = e15 * e16;

        // FSD E18: fallback use d03 from rows; if no rows, fall back to manual input.
        int e18 = d03 > 0 ? d03 : (input.SetAvgRoomSizeUnits ?? 0);
        // FSD E19: Avg indoor sales area per unit
        decimal e19 = e18 > 0 ? e09 / e18 : 0m;

        // FSD E20: Furniture per unit
        decimal e20 = input.FurniturePerUnit ?? 0m;
        // FSD E21: Furniture quantity — follows same fallback as E18
        int e21 = d03 > 0 ? d03 : (input.SetAvgRoomSizeUnits ?? 0);
        // FSD E22: Furniture total
        decimal e22 = e20 * e21;

        // FSD E23: External utilities
        decimal e23 = input.ExternalUtilities ?? 0m;
        // FSD E24: External utilities total = E23
        decimal e24 = e23;

        // FSD E25: Hard cost contingency percent
        decimal e25 = input.HardCostContingencyPercent ?? 3m;
        // FSD E26: Hard cost contingency amount
        decimal e26 = (e17 + e22 + e24) * e25 / 100m;
        // FSD E27: Total hard cost
        decimal e27 = e17 + e22 + e24 + e26;

        // FSD E28: Estimated construction period months
        int e28 = input.EstConstructionPeriodMonths ?? 0;

        // ── Step 5: Soft Cost (E29-E45) ───────────────────────────────────
        // FSD E29: Professional fee per month
        decimal e29 = input.ProfessionalFeePerMonth ?? 0m;
        // FSD E30: Professional fee months = E28
        int e30 = e28;
        // FSD E31: Professional fee total
        decimal e31 = e29 * e30;

        // FSD E32: Admin cost per month
        decimal e32 = input.AdminCostPerMonth ?? 0m;
        // FSD E33: Admin cost months = E14
        int e33 = e14;
        // FSD E34: Admin cost total
        decimal e34 = e32 * e33;

        // FSD E35: Selling/adv percent
        decimal e35 = input.SellingAdvPercent ?? 0m;
        // FSD E36: Selling/adv total
        decimal e36 = e13 * e35 / 100m;

        // FSD E37: Title deed fee
        decimal e37 = input.TitleDeedFee ?? 0m;
        // FSD E38: Title deed fee total = E37
        decimal e38 = e37;

        // FSD E39: EIA cost
        decimal e39 = input.EIACost ?? 0m;
        // FSD E40: EIA cost total = E39
        decimal e40 = e39;

        // FSD E41: Condo registration fee
        decimal e41 = input.CondoRegistrationFee ?? 0m;
        // FSD E42: Condo registration fee total = E41
        decimal e42 = e41;

        // FSD E43: Other expenses percent
        decimal e43 = input.OtherExpensesPercent ?? 0m;
        // FSD E44: Other expenses total
        decimal e44 = (e31 + e34 + e36 + e38 + e40 + e42) * e43 / 100m;
        // FSD E45: Total soft cost
        decimal e45 = e31 + e34 + e36 + e38 + e40 + e42 + e44;

        // ── Step 6: Government Taxes (E46-E50) ────────────────────────────
        // FSD E46: Transfer fee percent
        decimal e46 = input.TransferFeePercent ?? 1m;
        // FSD E47: Transfer fee total
        decimal e47 = e13 * e46 / 100m;

        // FSD E48: Specific biz tax percent
        decimal e48 = input.SpecificBizTaxPercent ?? 0m;
        // FSD E49: Specific biz tax total
        decimal e49 = e13 * e48 / 100m;

        // FSD E50: Total gov tax
        decimal e50 = e47 + e49;

        // ── Step 7: Risk Profit (E51-E52) ─────────────────────────────────
        // FSD E51: Risk profit percent
        decimal e51 = input.RiskProfitPercent ?? 0m;
        // FSD E52: Risk profit total
        decimal e52 = e13 * e51 / 100m;

        // ── Step 8: Total dev costs (E53) ─────────────────────────────────
        // FSD E53: Total dev costs
        decimal e53 = e27 + e45 + e50 + e52;

        // ── Step 9: Final value (E54-E59) ─────────────────────────────────
        // FSD E55: Discount rate
        decimal e55 = input.DiscountRate ?? 0m;

        // FSD E54: Total remaining value
        // if E55=0 → residual unchanged; else apply E55 as a percentage factor.
        decimal e54;
        if (e55 == 0m)
            e54 = e13 - e53;
        else
            e54 = (e13 - e53) * (e55 / 100m);

        // FSD E56: Discount rate factor (Reading 2 — literal FSD precedence):
        //   E56 = 1 / (1 + (E55/100)^(E14/12))
        decimal e56;
        if (e55 == 0m)
        {
            e56 = 1m;
        }
        else
        {
            var innerPow = (double)(e55 / 100m);
            var exponent = (double)e14 / 12.0;
            e56 = 1m / (1m + (decimal)Math.Pow(innerPow, exponent));
        }

        // FSD E57: Final remaining value
        decimal e57 = e54 * e56;
        // FSD E58: Total asset value rounded to nearest 10,000
        decimal e58 = RoundToNearest(e57, 10000m);
        // FSD E59: Total asset value per SqM rounded to nearest 100
        decimal e59 = e05 > 0m ? RoundToNearest(e58 / e05, 100m) : 0m;

        return new CondominiumSummary
        {
            AreaTitleDeed = e01,                         // FSD E01
            AreaSqM = e02,                               // FSD E02
            FAR = e03,                                   // FSD E03
            ConstructionAreaCityPlan = e04,              // FSD E04
            TotalBuildingArea = e05,                     // FSD E05
            CommonAreaPercent = e06,                     // FSD E06
            CommonArea = e07,                            // FSD E07
            IndoorSalesAreaPercent = e08,                // FSD E08
            IndoorSalesArea = e09,                       // FSD E09
            ProjectSalesArea = e10,                      // FSD E10
            AveragePricePerSqM = e11,                    // FSD E11
            TotalProjectSellingPrice = e12,              // FSD E12
            TotalRevenue = e13,                          // FSD E13
            EstSalesDurationMonths = e14,                // FSD E14
            CondoBuildingCostPerSqM = e15,               // FSD E15
            BuildingArea = e16,                          // FSD E16
            CondoBuildingCostTotal = e17,                // FSD E17
            SetAvgRoomSizeUnits = e18,                   // FSD E18
            AvgIndoorSalesAreaPerUnit = e19,             // FSD E19
            FurniturePerUnit = e20,                      // FSD E20
            FurnitureQuantity = e21,                     // FSD E21
            FurnitureTotal = e22,                        // FSD E22
            ExternalUtilities = e23,                     // FSD E23
            ExternalUtilitiesTotal = e24,                // FSD E24
            HardCostContingencyPercent = e25,            // FSD E25
            HardCostContingencyAmount = e26,             // FSD E26
            TotalHardCost = e27,                         // FSD E27
            EstConstructionPeriodMonths = e28,           // FSD E28
            ProfessionalFeePerMonth = e29,               // FSD E29
            ProfessionalFeeMonths = e30,                 // FSD E30
            ProfessionalFeeTotal = e31,                  // FSD E31
            AdminCostPerMonth = e32,                     // FSD E32
            AdminCostMonths = e33,                       // FSD E33
            AdminCostTotal = e34,                        // FSD E34
            SellingAdvPercent = e35,                     // FSD E35
            SellingAdvTotal = e36,                       // FSD E36
            TitleDeedFee = e37,                          // FSD E37
            TitleDeedFeeTotal = e38,                     // FSD E38
            EIACost = e39,                               // FSD E39
            EIACostTotal = e40,                          // FSD E40
            CondoRegistrationFee = e41,                  // FSD E41
            CondoRegistrationFeeTotal = e42,             // FSD E42
            OtherExpensesPercent = e43,                  // FSD E43
            OtherExpensesTotal = e44,                    // FSD E44
            TotalSoftCost = e45,                         // FSD E45
            TransferFeePercent = e46,                    // FSD E46
            TransferFeeTotal = e47,                      // FSD E47
            SpecificBizTaxPercent = e48,                 // FSD E48
            SpecificBizTaxTotal = e49,                   // FSD E49
            TotalGovTax = e50,                           // FSD E50
            RiskProfitPercent = e51,                     // FSD E51
            RiskProfitTotal = e52,                       // FSD E52
            TotalDevCosts = e53,                         // FSD E53
            TotalRemainingValue = e54,                   // FSD E54
            DiscountRate = e55,                          // FSD E55
            DiscountRateFactor = e56,                    // FSD E56
            FinalRemainingValue = e57,                   // FSD E57
            TotalAssetValueRounded = e58,                // FSD E58
            TotalAssetValuePerSqM = e59,                 // FSD E59
            Remark = input.Remark
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private static Dictionary<string, LandBuildingModelAggregate> AggregateModels(
        IReadOnlyList<LandBuildingUnitRow> rows)
    {
        var models = new Dictionary<string, LandBuildingModelAggregate>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var modelName = row.ModelName ?? "Unknown";
            if (!models.TryGetValue(modelName, out var agg))
            {
                agg = new LandBuildingModelAggregate { ModelName = modelName };
                models[modelName] = agg;
            }

            agg.UnitCount++;
            agg.TotalLandAreaSqWa += row.LandAreaSqWa ?? 0m;
            agg.TotalSellingPrice += row.SellingPrice ?? 0m;
        }

        // Average land area per model
        foreach (var agg in models.Values)
        {
            agg.AvgLandAreaSqWa = agg.UnitCount > 0
                ? agg.TotalLandAreaSqWa / agg.UnitCount
                : 0m;
        }

        return models;
    }

    /// <summary>
    /// Returns the sum of amounts for cost items matching (category, kind).
    /// Lookup is by stable <see cref="CostItemKind"/>, not by description text.
    /// </summary>
    private static decimal GetProjectCostAmount(
        IReadOnlyList<HypothesisCostItem> costItems,
        HypothesisCostCategory category,
        CostItemKind kind)
    {
        return costItems
            .Where(i => i.Category == category && i.Kind == kind)
            .Sum(i => i.Amount);
    }

    /// <summary>
    /// Rounds a value to the nearest multiple.
    /// RoundToNearest(123456, 10000) = 120000; RoundToNearest(125000, 10000) = 130000.
    /// Uses MidpointRounding.AwayFromZero.
    /// </summary>
    private static decimal RoundToNearest(decimal value, decimal nearest)
    {
        if (nearest == 0m) return value;
        return Math.Round(value / nearest, 0, MidpointRounding.AwayFromZero) * nearest;
    }

    // ── Result types ──────────────────────────────────────────────────────

    public record LandBuildingSnapshot(
        LandBuildingSummary Summary,
        Dictionary<string, LandBuildingModelAggregate> Models);

    /// <summary>
    /// Per-model aggregate data (A-fields, per-model C11-C26).
    /// Computed at calculation time, not persisted — derived from unit rows.
    /// </summary>
    public class LandBuildingModelAggregate
    {
        public string ModelName { get; set; } = null!;
        public int UnitCount { get; set; }
        public decimal AvgLandAreaSqWa { get; set; }
        public decimal TotalLandAreaSqWa { get; set; }
        public decimal TotalSellingPrice { get; set; }

        /// <summary>FSD C19/C23/... — Total value after depreciation per unit (from cost items).</summary>
        public decimal TotalValueAfterDepreciation { get; set; }

        /// <summary>FSD C21/C25/... — Total value after depreciation × unit count.</summary>
        public decimal TotalValueAfterDepreciationAllUnits { get; set; }

        /// <summary>FSD C22/C26/... — Dev cost ratio (%).</summary>
        public decimal DevCostRatioPercent { get; set; }
    }
}
