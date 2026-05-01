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
/// Discount rate factor (confirmed PV formula):
///   C79 = 1 / (1 + C78/100) ^ (C18/12)
///   E56 = 1 / (1 + E55/100) ^ (E14/12)
///
/// C77 conditional:
///   if C78 = 0: C77 = C15 - C76
///   if C78 ≠ 0: C77 = (C15 – C76) × C78
///   Note: when C78 ≠ 0 the FSD formula multiplies the residual by C78 as a numeric factor (not a percent division).
///   C80 = C77 × C79 so the double-application is intentional per FSD.
///
/// E54 conditional (mirrors C77):
///   if E55 = 0: E54 = E13 – E53
///   if E55 ≠ 0: E54 = (E13 – E53) × E55
/// </summary>
public class HypothesisCalculationService
{
    // ── L&B public API ────────────────────────────────────────────────────

    /// <summary>
    /// Computes all L&B C-field formulas given the analysis and the per-model cost items.
    /// Populates and returns an updated <see cref="LandBuildingSummary"/>.
    /// The per-model detail snapshot (A-fields, per-model C11-C26) is included in the result.
    /// </summary>
    public LandBuildingSnapshot ComputeLandBuilding(
        HypothesisAnalysis analysis,
        IReadOnlyList<LandBuildingUnitRow> rows,
        LandBuildingSummary input)
    {
        // ── Step 1: Aggregate per-model A/C fields from unit rows ─────────
        var models = AggregateModels(rows);

        // ── Step 2: Area calculations (C01-C10A) ──────────────────────────
        decimal c01 = input.C01TotalArea ?? 0m;
        decimal c02 = input.C02SellingAreaPercent ?? 0m;
        decimal c03 = models.Values.Sum(m => m.TotalLandAreaSqWa); // SUM of per-model total areas
        decimal c10 = input.C10PublicUtilityAreaPercent ?? 0m;
        decimal c10a = c10 / 100m * c01;

        // ── Step 3: Revenue (C11-C15) ─────────────────────────────────────
        // Per-model: C11 = units, C12 = total selling price (summed from rows)
        decimal c15 = models.Values.Sum(m => m.TotalSellingPrice);
        int c17 = models.Values.Sum(m => m.UnitCount); // total units
        int c16 = input.C16EstSalesPeriod ?? 1;
        int c18 = c16 > 0 ? (int)Math.Ceiling((double)c17 / c16) : 0;

        // ── Step 4: Per-model construction cost from cost items ───────────
        var costItems = analysis.CostItems;
        decimal sumBuildingCostAllModels = 0m; // C21+C25+...

        foreach (var model in models.Values)
        {
            // C19 = total value after depreciation for this model (from cost-of-building items)
            decimal c19 = costItems
                .Where(i => i.Category == HypothesisCostCategory.CostOfBuilding
                             && i.ModelName == model.ModelName)
                .Sum(i => i.Amount);
            model.TotalValueAfterDepreciation = c19;
            // C21 = C19 × C20 (C20 = unit count for the model)
            model.TotalValueAfterDepreciationAllUnits = c19 * model.UnitCount;
            sumBuildingCostAllModels += model.TotalValueAfterDepreciationAllUnits;
        }

        // ── Step 5: Project Dev Cost (C27-C39) ────────────────────────────
        decimal c27 = input.C27PublicUtilityRatePerSqWa ?? 0m;
        decimal c28 = c01; // = C01
        decimal c29 = c27 * c28;

        decimal c31 = input.C31LandFillingRatePerSqWa ?? 0m;
        decimal c32 = c01; // = C01
        decimal c33 = c31 * c32;

        decimal c35 = input.C35ContingencyPercent ?? 3m;
        decimal c36 = (sumBuildingCostAllModels + c29 + c33) * c35 / 100m;

        decimal c38 = sumBuildingCostAllModels + c29 + c33 + c36;
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
        int c40 = input.C40EstConstructionPeriod ?? 1;
        int c41 = c17; // = C17
        int c42 = c40 > 0 ? (int)Math.Ceiling((double)c41 / c40) : 0;

        // ── Step 7: Project Cost (C43-C65) ────────────────────────────────
        // Allocation permit fee
        decimal c43 = GetProjectCostAmount(costItems, HypothesisCostCategory.ProjectCost, "AllocationPermitFee");
        decimal c44 = c43; // = C43

        // Land title deed division fee
        decimal c46 = input.C46LandTitleFeePerPlot ?? 0m;
        int c47 = c41; // = C41
        decimal c48 = c46 * c47;

        // Professional service fees
        decimal c50 = input.C50ProfessionalFeePerMonth ?? 0m;
        int c51 = c42; // = C42
        decimal c52 = c50 * c51;

        // Admin costs
        decimal c54 = input.C54AdminCostPerMonth ?? 0m;
        int c55 = c18; // = C18
        decimal c56 = c54 * c55;

        // Selling/Adv
        decimal c58 = input.C58SellingAdvPercent ?? 0m;
        decimal c59 = c15 * c58 / 100m;

        decimal c61 = input.C61ProjectContingencyPercent ?? 3m;
        decimal c62 = (c44 + c48 + c52 + c56 + c59) * c61 / 100m;

        decimal c64 = c44 + c48 + c52 + c56 + c59 + c62;
        decimal c65 = c64 > 0 ? 100m : 0m;

        // Ratios
        decimal c45 = c64 > 0 ? c44 * 100m / c64 : 0m;
        decimal c49 = c64 > 0 ? c48 * 100m / c64 : 0m;
        decimal c53 = c64 > 0 ? c52 * 100m / c64 : 0m;
        decimal c57 = c64 > 0 ? c56 * 100m / c64 : 0m;
        decimal c60 = c64 > 0 ? c59 * 100m / c64 : 0m;
        decimal c63 = c64 > 0 ? c62 * 100m / c64 : 0m;

        // ── Step 8: Government Taxes (C66-C73) ────────────────────────────
        decimal c66 = input.C66TransferFeePercent ?? 0m;
        decimal c67 = c15 * c66 / 100m;

        decimal c69 = input.C69SpecificBizTaxPercent ?? 0m;
        decimal c70 = c15 * c69 / 100m;

        decimal c72 = c67 + c70;
        decimal c73 = c72 > 0 ? 100m : 0m;
        decimal c68 = c72 > 0 ? c67 * 100m / c72 : 0m;
        decimal c71 = c72 > 0 ? c70 * 100m / c72 : 0m;

        // ── Step 9: Risk Premium (C74-C75) ────────────────────────────────
        decimal c74 = input.C74RiskPremiumPercent ?? 0m;
        decimal c75 = c15 * c74 / 100m;

        // ── Step 10: Total dev costs (C76) ────────────────────────────────
        decimal c76 = c38 + c64 + c72 + c75;

        // ── Step 11: Current property value (C77-C82) ─────────────────────
        decimal c78 = input.C78DiscountRate ?? 0m;

        decimal c77;
        if (c78 == 0m)
            c77 = c15 - c76;
        else
            c77 = (c15 - c76) * c78;

        decimal c79 = c78 > 0m
            ? 1m / (decimal)Math.Pow((double)(1m + c78 / 100m), (double)c18 / 12.0)
            : 1m;

        decimal c80 = c77 * c79;
        decimal c81 = RoundToNearest(c80, 10000m);
        decimal c82 = c01 > 0m ? RoundToNearest(c81 / c01, 100m) : 0m;

        // ── Build the updated summary ─────────────────────────────────────
        var summary = new LandBuildingSummary
        {
            C01TotalArea = c01,
            C02SellingAreaPercent = c02,
            C03SellingArea = c03,
            C10PublicUtilityAreaPercent = c10,
            C10APublicUtilityArea = c10a,
            C15TotalRevenue = c15,
            C16EstSalesPeriod = c16,
            C17TotalUnits = c17,
            C18EstimatedDurationMonths = c18,
            C27PublicUtilityRatePerSqWa = c27,
            C28PublicUtilityArea = c28,
            C29PublicUtilityCost = c29,
            C30PublicUtilityCostRatio = c30,
            C31LandFillingRatePerSqWa = c31,
            C32LandFillingArea = c32,
            C33LandFillingCost = c33,
            C34LandFillingCostRatio = c34,
            C35ContingencyPercent = c35,
            C36ContingencyAmount = c36,
            C37ContingencyRatio = c37,
            C38TotalProjectDevCost = c38,
            C39TotalDevCostRatio = c39,
            C40EstConstructionPeriod = c40,
            C41TotalUnits = c41,
            C42EstimatedDurationMonths = c42,
            C44AllocationPermitFee = c44,
            C45AllocationPermitFeeRatio = c45,
            C46LandTitleFeePerPlot = c46,
            C47TotalPlots = c47,
            C48LandTitleFeeTotal = c48,
            C49LandTitleFeeRatio = c49,
            C50ProfessionalFeePerMonth = c50,
            C51ProfessionalFeeMonths = c51,
            C52ProfessionalFeeTotal = c52,
            C53ProfessionalFeeRatio = c53,
            C54AdminCostPerMonth = c54,
            C55AdminCostMonths = c55,
            C56AdminCostTotal = c56,
            C57AdminCostRatio = c57,
            C58SellingAdvPercent = c58,
            C59SellingAdvTotal = c59,
            C60SellingAdvRatio = c60,
            C61ProjectContingencyPercent = c61,
            C62ProjectContingencyAmount = c62,
            C63ProjectContingencyRatio = c63,
            C64TotalProjectCost = c64,
            C65TotalProjectCostRatio = c65,
            C66TransferFeePercent = c66,
            C67TransferFeeAmount = c67,
            C68TransferFeeRatio = c68,
            C69SpecificBizTaxPercent = c69,
            C70SpecificBizTaxAmount = c70,
            C71SpecificBizTaxRatio = c71,
            C72TotalGovTax = c72,
            C73TotalGovTaxRatio = c73,
            C74RiskPremiumPercent = c74,
            C75RiskPremiumAmount = c75,
            C76TotalDevCostsAndExpenses = c76,
            C77CurrentPropertyValue = c77,
            C78DiscountRate = c78,
            C79DiscountRateFactor = c79,
            C80FinalPropertyValue = c80,
            C81TotalAssetValueRounded = c81,
            C82TotalAssetValuePerSqWa = c82,
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
        // ── Step 1: Aggregate from upload (D01-D04 → E01/E09/E12/E18) ─────
        decimal d01 = input.E01AreaTitleDeed ?? rows.FirstOrDefault()?.UsableAreaSqM ?? 0m; // title deed from input
        decimal d02 = rows.Sum(r => r.UsableAreaSqM ?? 0m); // total indoor sales area
        int d03 = rows.Count; // total units
        decimal d04 = rows.Sum(r => r.SellingPrice ?? 0m); // total selling price

        // ── Step 2: Land area details ─────────────────────────────────────
        decimal e01 = input.E01AreaTitleDeed ?? 0m;
        decimal e02 = e01 * 4m;
        decimal e03 = input.E03FAR ?? 0m;
        decimal e04 = e03 > 0m ? Math.Round(e02 * e03, 0, MidpointRounding.AwayFromZero) : 0m;
        decimal e05 = input.E05TotalBuildingArea ?? 0m;

        decimal e09 = d02; // from upload
        decimal e08 = e05 > 0m ? e09 * 100m / e05 : 0m;
        decimal e06 = 100m - e08;
        decimal e07 = e05 - e09;

        // ── Step 3: Revenue (E10-E13) ─────────────────────────────────────
        decimal e10 = e09;
        decimal e12 = d04;
        decimal e11 = e10 > 0m ? e12 / e10 : 0m;
        decimal e13 = e12;

        int e14 = input.E14EstSalesDurationMonths ?? 0;

        // ── Step 4: Hard Cost (E15-E27) ───────────────────────────────────
        decimal e15 = input.E15CondoBuildingCostPerSqM ?? 0m;
        decimal e16 = e05;
        decimal e17 = e15 * e16;

        int e18 = d03 > 0 ? d03 : (input.E18SetAvgRoomSizeUnits ?? 0);
        decimal e19 = e18 > 0 ? e09 / e18 : 0m;

        decimal e20 = input.E20FurniturePerUnit ?? 0m;
        int e21 = d03;
        decimal e22 = e20 * e21;

        decimal e23 = input.E23ExternalUtilities ?? 0m;
        decimal e24 = e23;

        decimal e25 = input.E25HardCostContingencyPercent ?? 3m;
        decimal e26 = (e17 + e22 + e24) * e25 / 100m;
        decimal e27 = e17 + e22 + e24 + e26;

        int e28 = input.E28EstConstructionPeriodMonths ?? 0;

        // ── Step 5: Soft Cost (E29-E45) ───────────────────────────────────
        decimal e29 = input.E29ProfessionalFeePerMonth ?? 0m;
        int e30 = e28;
        decimal e31 = e29 * e30;

        decimal e32 = input.E32AdminCostPerMonth ?? 0m;
        int e33 = e14;
        decimal e34 = e32 * e33;

        decimal e35 = input.E35SellingAdvPercent ?? 0m;
        decimal e36 = e13 * e35 / 100m;

        decimal e37 = input.E37TitleDeedFee ?? 0m;
        decimal e38 = e37;

        decimal e39 = input.E39EIACost ?? 0m;
        decimal e40 = e39;

        decimal e41 = input.E41CondoRegistrationFee ?? 0m;
        decimal e42 = e41;

        decimal e43 = input.E43OtherExpensesPercent ?? 0m;
        decimal e44 = (e31 + e34 + e36 + e38 + e40 + e42) * e43 / 100m;
        decimal e45 = e31 + e34 + e36 + e38 + e40 + e42 + e44;

        // ── Step 6: Government Taxes (E46-E50) ────────────────────────────
        decimal e46 = input.E46TransferFeePercent ?? 1m;
        decimal e47 = e13 * e46 / 100m;

        decimal e48 = input.E48SpecificBizTaxPercent ?? 0m;
        decimal e49 = e13 * e48 / 100m;

        decimal e50 = e47 + e49;

        // ── Step 7: Risk Profit (E51-E52) ─────────────────────────────────
        decimal e51 = input.E51RiskProfitPercent ?? 0m;
        decimal e52 = e13 * e51 / 100m;

        // ── Step 8: Total dev costs (E53) ─────────────────────────────────
        decimal e53 = e27 + e45 + e50 + e52;

        // ── Step 9: Final value (E54-E59) ─────────────────────────────────
        decimal e55 = input.E55DiscountRate ?? 0m;

        decimal e54;
        if (e55 == 0m)
            e54 = e13 - e53;
        else
            e54 = (e13 - e53) * e55;

        decimal e56 = e55 > 0m
            ? 1m / (decimal)Math.Pow((double)(1m + e55 / 100m), (double)e14 / 12.0)
            : 1m;

        decimal e57 = e54 * e56;
        decimal e58 = RoundToNearest(e57, 10000m);
        decimal e59 = e05 > 0m ? RoundToNearest(e58 / e05, 100m) : 0m;

        return new CondominiumSummary
        {
            E01AreaTitleDeed = e01,
            E02AreaSqM = e02,
            E03FAR = e03,
            E04ConstructionAreaCityPlan = e04,
            E05TotalBuildingArea = e05,
            E06CommonAreaPercent = e06,
            E07CommonArea = e07,
            E08IndoorSalesAreaPercent = e08,
            E09IndoorSalesArea = e09,
            E10ProjectSalesArea = e10,
            E11AveragePricePerSqM = e11,
            E12TotalProjectSellingPrice = e12,
            E13TotalRevenue = e13,
            E14EstSalesDurationMonths = e14,
            E15CondoBuildingCostPerSqM = e15,
            E16BuildingArea = e16,
            E17CondoBuildingCostTotal = e17,
            E18SetAvgRoomSizeUnits = e18,
            E19AvgIndoorSalesAreaPerUnit = e19,
            E20FurniturePerUnit = e20,
            E21FurnitureQuantity = e21,
            E22FurnitureTotal = e22,
            E23ExternalUtilities = e23,
            E24ExternalUtilitiesTotal = e24,
            E25HardCostContingencyPercent = e25,
            E26HardCostContingencyAmount = e26,
            E27TotalHardCost = e27,
            E28EstConstructionPeriodMonths = e28,
            E29ProfessionalFeePerMonth = e29,
            E30ProfessionalFeeMonths = e30,
            E31ProfessionalFeeTotal = e31,
            E32AdminCostPerMonth = e32,
            E33AdminCostMonths = e33,
            E34AdminCostTotal = e34,
            E35SellingAdvPercent = e35,
            E36SellingAdvTotal = e36,
            E37TitleDeedFee = e37,
            E38TitleDeedFeeTotal = e38,
            E39EIACost = e39,
            E40EIACostTotal = e40,
            E41CondoRegistrationFee = e41,
            E42CondoRegistrationFeeTotal = e42,
            E43OtherExpensesPercent = e43,
            E44OtherExpensesTotal = e44,
            E45TotalSoftCost = e45,
            E46TransferFeePercent = e46,
            E47TransferFeeTotal = e47,
            E48SpecificBizTaxPercent = e48,
            E49SpecificBizTaxTotal = e49,
            E50TotalGovTax = e50,
            E51RiskProfitPercent = e51,
            E52RiskProfitTotal = e52,
            E53TotalDevCosts = e53,
            E54TotalRemainingValue = e54,
            E55DiscountRate = e55,
            E56DiscountRateFactor = e56,
            E57FinalRemainingValue = e57,
            E58TotalAssetValueRounded = e58,
            E59TotalAssetValuePerSqM = e59,
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

    private static decimal GetProjectCostAmount(
        IReadOnlyList<HypothesisCostItem> costItems,
        HypothesisCostCategory category,
        string description)
    {
        return costItems
            .Where(i => i.Category == category && i.Description.Contains(description, StringComparison.OrdinalIgnoreCase))
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

        /// <summary>C19/C23/... — Total value after depreciation per unit (from cost items).</summary>
        public decimal TotalValueAfterDepreciation { get; set; }

        /// <summary>C21/C25/... — Total value after depreciation × unit count.</summary>
        public decimal TotalValueAfterDepreciationAllUnits { get; set; }

        /// <summary>C22/C26/... — Dev cost ratio (%).</summary>
        public decimal DevCostRatioPercent { get; set; }
    }
}
