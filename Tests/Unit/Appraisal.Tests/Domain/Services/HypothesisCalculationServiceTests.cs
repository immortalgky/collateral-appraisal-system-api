using Appraisal.Domain.Appraisals.Hypothesis;
using Appraisal.Domain.Appraisals.Hypothesis.CostItems;
using Appraisal.Domain.Appraisals.Hypothesis.Summaries;
using Appraisal.Domain.Appraisals.Hypothesis.Uploads;
using Appraisal.Domain.Services;

namespace Appraisal.Tests.Domain.Services;

/// <summary>
/// Unit tests for <see cref="HypothesisCalculationService"/> FSD §2.1.3.7 formulas.
///
/// Key invariants under test:
///   - FSD C77: C15-C76 (no discount applied; discount handled via C79 factor)
///   - FSD C79: standard PV factor — 1 / (1 + C78/100)^(C18/12)
///   - FSD C81: Round(C80, 10000)
///   - FSD C82: Round(C81/C01, 100)
///   - FSD E54: E13-E53 (mirrors C77)
///   - FSD E56: standard PV factor — 1 / (1 + E55/100)^(E14/12)
///   - FSD E58: Round(E57, 10000)
///   - FSD E59: Round(E58/E05, 100)
///   - SetAmounts rejects negative amount
///   - CostItem lookup by Kind is description-independent
///   - E21 follows same fallback as E18
/// </summary>
public class HypothesisCalculationServiceTests
{
    private static readonly HypothesisCalculationService Sut = new();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static HypothesisAnalysis CreateLandBuildingAnalysis()
        => HypothesisAnalysis.Create(Guid.NewGuid(), HypothesisVariant.LandBuilding);

    private static HypothesisAnalysis CreateCondoAnalysis()
        => HypothesisAnalysis.Create(Guid.NewGuid(), HypothesisVariant.Condominium);

    private static LandBuildingUnitRow MakeLbRow(string model, decimal landArea, decimal price)
        => LandBuildingUnitRow.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "P1", "H1", model, null, null, landArea, null, price, null, null);

    private static CondominiumUnitRow MakeCondoRow(decimal usableArea, decimal price)
        => CondominiumUnitRow.Create(Guid.NewGuid(), Guid.NewGuid(), 1, 1, "A", "1A", "Studio", null, usableArea, price, null, null);

    // ── RoundToNearest (via FSD C81/E58 output) ──────────────────────────────────

    [Theory]
    [InlineData(123456, 10000, 120000)]   // round down
    [InlineData(125000, 10000, 130000)]   // midpoint rounds up (AwayFromZero)
    [InlineData(127000, 10000, 130000)]   // round up
    [InlineData(0, 10000, 0)]             // zero
    public void RoundToNearest_10000_ProducesExpectedC81(decimal raw, decimal nearest, decimal expected)
    {
        var analysis = CreateLandBuildingAnalysis();
        var rows = new List<LandBuildingUnitRow>
        {
            MakeLbRow("M1", 50m, raw)
        };

        var input = new LandBuildingSummary
        {
            TotalArea = 1000m,              // FSD C01
            EstSalesPeriod = 1,             // FSD C16
            DiscountRate = 0m               // FSD C78
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input);

        Assert.Equal(expected, result.Summary.TotalAssetValueRounded); // FSD C81
        _ = nearest;
    }

    // ── L&B: Zero discount rate ───────────────────────────────────────────────

    [Fact]
    public void LandBuilding_ZeroDiscountRate_C77EqualsC15MinusC76_C79EqualsOne()
    {
        // FSD C78 = 0 → C77 = C15 - C76; C79 = 1; C80 = C77
        var analysis = CreateLandBuildingAnalysis();

        var rows = new[]
        {
            MakeLbRow("TypeA", 100m, 1_000_000m),
            MakeLbRow("TypeA", 100m, 1_000_000m)
        };

        var costItem = analysis.AddCostItem(HypothesisCostCategory.CostOfBuilding,
            CostItemKind.BuildingConstruction, "Construction", 1, "TypeA");
        costItem.SetAmounts(300_000m, 0m, 0, 0m);

        var input = new LandBuildingSummary
        {
            TotalArea = 500m,                    // FSD C01
            EstSalesPeriod = 1,                  // FSD C16
            ContingencyPercent = 3m,             // FSD C35
            ProjectContingencyPercent = 3m,      // FSD C61
            DiscountRate = 0m                    // FSD C78
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input);
        var s = result.Summary;

        Assert.Equal(0m, s.DiscountRate);           // FSD C78
        Assert.Equal(1m, s.DiscountRateFactor);     // FSD C79

        decimal expectedC15 = 2_000_000m;
        Assert.Equal(expectedC15, s.TotalRevenue);  // FSD C15

        var expectedC77 = s.TotalRevenue!.Value - s.TotalDevCostsAndExpenses!.Value;
        Assert.Equal(expectedC77, s.CurrentPropertyValue); // FSD C77

        Assert.Equal(expectedC77, s.FinalPropertyValue);   // FSD C80
    }

    [Fact]
    public void LandBuilding_ZeroDiscountRate_C81AndC82AreRounded()
    {
        var analysis = CreateLandBuildingAnalysis();
        var rows = new[] { MakeLbRow("T1", 100m, 5_125_000m) };

        var input = new LandBuildingSummary
        {
            TotalArea = 100m,                    // FSD C01
            EstSalesPeriod = 1,                  // FSD C16
            ContingencyPercent = 0m,             // FSD C35
            ProjectContingencyPercent = 0m,      // FSD C61
            DiscountRate = 0m                    // FSD C78
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input);
        var s = result.Summary;

        Assert.Equal(5_130_000m, s.TotalAssetValueRounded);  // FSD C81
        Assert.Equal(51_300m, s.TotalAssetValuePerSqWa);     // FSD C82
    }

    // ── L&B: Non-zero discount rate (Reading 2 formulas) ──────────────────────

    [Fact]
    public void LandBuilding_NonZeroDiscountRate_C77IsResidual_C79IsStandardPV()
    {
        // FSD C78 = 10 (10%), C18 = 1 month (from 1 unit / 1 per period)
        // C77 = C15 - C76 (no discount applied here)
        // C79 (standard PV) = 1 / (1.10)^(1/12) ≈ 0.9921
        var analysis = CreateLandBuildingAnalysis();
        var rows = new[] { MakeLbRow("M1", 50m, 1_000_000m) };

        var input = new LandBuildingSummary
        {
            TotalArea = 200m,                    // FSD C01
            EstSalesPeriod = 1,                  // FSD C16
            ContingencyPercent = 0m,             // FSD C35
            EstConstructionPeriod = 1,           // FSD C40
            ProjectContingencyPercent = 0m,      // FSD C61
            DiscountRate = 10m                   // FSD C78
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input);
        var s = result.Summary;

        // FSD C18 = ceil(1/1) = 1
        Assert.Equal(1, s.EstimatedDurationMonths); // FSD C18

        // FSD C77 = C15 - C76 (no percentage applied — discount happens via C79)
        decimal expectedC77 = s.TotalRevenue!.Value - s.TotalDevCostsAndExpenses!.Value;
        Assert.Equal(expectedC77, s.CurrentPropertyValue); // FSD C77

        // FSD C79 standard PV: 1 / (1 + C78/100)^(C18/12) = 1 / (1.10)^(1/12)
        double expectedC79 = 1.0 / Math.Pow(1.10, 1.0 / 12.0);
        Assert.Equal(Math.Round((decimal)expectedC79, 6), Math.Round(s.DiscountRateFactor!.Value, 6));
        Assert.True(s.DiscountRateFactor < 1m);
    }

    [Fact]
    public void LandBuilding_NonZeroDiscountRate_C79IsStandardPV_NotLiteralFsdReading()
    {
        // At FSD C78=10, C18=24:
        //   Standard PV: 1/(1.10)^2 = 0.826
        //   Literal FSD (broken): 1/(1 + 0.10^2) = 1/(1.01) = 0.990
        // We use the standard PV interpretation (see project memory note).
        var analysis = CreateLandBuildingAnalysis();
        var manyRows = Enumerable.Range(1, 24).Select(_ => MakeLbRow("M1", 50m, 100_000m)).ToList();

        var input = new LandBuildingSummary
        {
            TotalArea = 200m,                    // FSD C01
            EstSalesPeriod = 1,                  // FSD C16 — C18 = ceil(24/1) = 24
            ContingencyPercent = 0m,             // FSD C35
            ProjectContingencyPercent = 0m,      // FSD C61
            DiscountRate = 10m                   // FSD C78
        };

        var result = Sut.ComputeLandBuilding(analysis, manyRows, input);
        var s = result.Summary;

        Assert.Equal(24, s.EstimatedDurationMonths); // FSD C18

        // Standard PV: 1 / (1.10)^(24/12) = 1 / 1.21 ≈ 0.826
        double expectedC79 = 1.0 / Math.Pow(1.10, 24.0 / 12.0);
        Assert.Equal(Math.Round((decimal)expectedC79, 6), Math.Round(s.DiscountRateFactor!.Value, 6));

        // Literal FSD reading would give ≈ 0.990 — assert we are NOT using it
        double literalReading = 1.0 / (1.0 + Math.Pow(0.10, 24.0 / 12.0));
        Assert.NotEqual(Math.Round((decimal)literalReading, 4), Math.Round(s.DiscountRateFactor!.Value, 4));
    }

    // ── L&B: Duration calculation (FSD C18) ──────────────────────────────────

    [Fact]
    public void LandBuilding_C18IsCalculatedFromC17DividedByC16_CeilingApplied()
    {
        var analysis = CreateLandBuildingAnalysis();
        var rows = new[]
        {
            MakeLbRow("M1", 100m, 500_000m),
            MakeLbRow("M1", 100m, 500_000m),
            MakeLbRow("M1", 100m, 500_000m),
            MakeLbRow("M1", 100m, 500_000m),
            MakeLbRow("M1", 100m, 500_000m)
        };

        var input = new LandBuildingSummary
        {
            TotalArea = 1000m,               // FSD C01
            EstSalesPeriod = 2,              // FSD C16
            ContingencyPercent = 0m,         // FSD C35
            ProjectContingencyPercent = 0m,  // FSD C61
            DiscountRate = 0m                // FSD C78
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input);

        Assert.Equal(5, result.Summary.TotalUnits);                    // FSD C17
        Assert.Equal(3, result.Summary.EstimatedDurationMonths);       // FSD C18
    }

    // ── L&B: Cost item aggregation ────────────────────────────────────────────

    [Fact]
    public void LandBuilding_PerModelBuildingCost_AggregatedCorrectly()
    {
        var analysis = CreateLandBuildingAnalysis();

        var rows = new[]
        {
            MakeLbRow("A", 60m, 800_000m),
            MakeLbRow("A", 60m, 800_000m),
            MakeLbRow("B", 80m, 1_000_000m)
        };

        var costA = analysis.AddCostItem(HypothesisCostCategory.CostOfBuilding,
            CostItemKind.BuildingConstruction, "Construction", 1, "A");
        costA.SetAmounts(200_000m, 0m, 0, 0m);

        var costB = analysis.AddCostItem(HypothesisCostCategory.CostOfBuilding,
            CostItemKind.BuildingConstruction, "Construction", 2, "B");
        costB.SetAmounts(300_000m, 0m, 0, 0m);

        var input = new LandBuildingSummary
        {
            TotalArea = 500m,                // FSD C01
            EstSalesPeriod = 1,              // FSD C16
            ContingencyPercent = 0m,         // FSD C35
            ProjectContingencyPercent = 0m,  // FSD C61
            DiscountRate = 0m                // FSD C78
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input);

        Assert.Equal(700_000m, result.Summary.TotalProjectDevCost); // FSD C38
        Assert.Equal(2, result.Models["A"].UnitCount);
        Assert.Equal(400_000m, result.Models["A"].TotalValueAfterDepreciationAllUnits);
        Assert.Equal(1, result.Models["B"].UnitCount);
        Assert.Equal(300_000m, result.Models["B"].TotalValueAfterDepreciationAllUnits);
    }

    // ── L&B: Public utility cost (C29 uses C10A — public utility area) ────────

    [Fact]
    public void LandBuilding_PublicUtilityCost_C29EqualsC27TimesC10A()
    {
        // FSD C28 = Public Utility Area (C10A), NOT total land area (C01).
        // With TotalArea=400 and PublicUtilityAreaPercent=75 → C10A = 300 SqWa.
        // C29 = C27 × C28 = 500 × 300 = 150,000.
        var analysis = CreateLandBuildingAnalysis();
        var rows = new[] { MakeLbRow("M1", 100m, 500_000m) };

        var input = new LandBuildingSummary
        {
            TotalArea = 400m,                        // FSD C01
            PublicUtilityAreaPercent = 75m,           // FSD C10 → C10A = 300
            EstSalesPeriod = 1,                       // FSD C16
            PublicUtilityRatePerSqWa = 500m,          // FSD C27
            ContingencyPercent = 0m,                  // FSD C35
            ProjectContingencyPercent = 0m,           // FSD C61
            DiscountRate = 0m                         // FSD C78
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input);

        Assert.Equal(300m, result.Summary.PublicUtilityAreaForCost); // FSD C28
        Assert.Equal(150_000m, result.Summary.PublicUtilityCost);    // FSD C29
    }

    // ── L&B: Allocation Permit Fee from summary input ─────────────────────────

    [Fact]
    public void LandBuilding_AllocationPermitFee_FromSummaryInput()
    {
        // FSD C44 is now driven by the summary input directly (no cost-item lookup).
        var analysis = CreateLandBuildingAnalysis();
        var rows = new[] { MakeLbRow("M1", 100m, 1_000_000m) };

        var input = new LandBuildingSummary
        {
            TotalArea = 100m,                // FSD C01
            EstSalesPeriod = 1,              // FSD C16
            ContingencyPercent = 0m,         // FSD C35
            AllocationPermitFee = 50_000m,   // FSD C44
            ProjectContingencyPercent = 0m,  // FSD C61
            DiscountRate = 0m                // FSD C78
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input);

        Assert.Equal(50_000m, result.Summary.AllocationPermitFee); // FSD C44
    }

    // ── L&B: Allocation Permit Fee ignores legacy cost-item rows ──────────────

    [Fact]
    public void LandBuilding_AllocationPermitFee_LegacyCostItemRow_IsIgnored()
    {
        // Regression guard: prior implementations read C44 from a ProjectCost
        // cost item with Kind == AllocationPermitFee. We removed that path. If a
        // legacy aggregate happens to still carry such a row (e.g. mid-migration
        // data), it must NOT contribute to C44 — the summary input is the only
        // source.
        var analysis = CreateLandBuildingAnalysis();
        var rows = new[] { MakeLbRow("M1", 100m, 1_000_000m) };

        // Legacy cost-item row with a large amount that would have driven C44 before.
        var legacyRow = analysis.AddCostItem(
            HypothesisCostCategory.ProjectCost,
            CostItemKind.AllocationPermitFee,
            "Legacy Permit Fee",
            displaySequence: 99);
        legacyRow.SetAmounts(999_999m);

        var input = new LandBuildingSummary
        {
            TotalArea = 100m,
            EstSalesPeriod = 1,
            ContingencyPercent = 0m,
            AllocationPermitFee = 50_000m,   // summary input wins
            ProjectContingencyPercent = 0m,
            DiscountRate = 0m
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input);

        // C44 must equal the summary input, NOT the legacy cost-item amount.
        Assert.Equal(50_000m, result.Summary.AllocationPermitFee);
    }

    // ── L&B: User-added Project Dev Cost rows feed into C36 base + C38 ────────

    [Fact]
    public void LandBuilding_UserAddedProjectDevCostRow_IsIncludedInC36BaseAndC38()
    {
        // No building / public-utility / land-filling costs, and contingency = 10%.
        // A single user-added Project Dev Cost row of 1,000,000:
        //   C36 base = 1,000,000 → C36 = 100,000
        //   C38 = 1,000,000 + 100,000 = 1,100,000
        //   Per-row CategoryRatio = 1,000,000 × 100 / 1,100,000 ≈ 90.909%
        var analysis = CreateLandBuildingAnalysis();
        var rows = new[] { MakeLbRow("M1", 50m, 1_000_000m) };

        var userRow = analysis.AddCostItem(
            HypothesisCostCategory.ProjectDevCost,
            CostItemKind.Other,
            "Custom Site Prep",
            displaySequence: 99);
        userRow.SetAmounts(1_000_000m);

        var input = new LandBuildingSummary
        {
            TotalArea = 200m,                      // FSD C01
            EstSalesPeriod = 1,                    // FSD C16
            ContingencyPercent = 10m,              // FSD C35
            ProjectContingencyPercent = 0m,        // FSD C61
            DiscountRate = 0m                      // FSD C78
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input);
        var s = result.Summary;

        Assert.Equal(100_000m, s.ContingencyAmount);          // FSD C36
        Assert.Equal(1_100_000m, s.TotalProjectDevCost);      // FSD C38

        // Per-row CategoryRatio stamped on the persisted entity
        var stamped = analysis.CostItems.Single(i =>
            i.Category == HypothesisCostCategory.ProjectDevCost
            && i.Kind == CostItemKind.Other);
        Assert.NotNull(stamped.CategoryRatio);
        Assert.Equal(
            Math.Round(1_000_000m * 100m / 1_100_000m, 4),
            Math.Round(stamped.CategoryRatio!.Value, 4));
    }

    // ── L&B: User-added Project Cost rows feed into C62 base + C64 ────────────

    [Fact]
    public void LandBuilding_UserAddedProjectCostRow_IsIncludedInC62BaseAndC64()
    {
        // Single user-added Project Cost row of 500,000, contingency = 10%.
        //   C62 base = 500,000 → C62 = 50,000
        //   C64 = 500,000 + 50,000 = 550,000
        //   Per-row CategoryRatio = 500,000 × 100 / 550,000 ≈ 90.909%
        var analysis = CreateLandBuildingAnalysis();
        var rows = new[] { MakeLbRow("M1", 50m, 1_000_000m) };

        var userRow = analysis.AddCostItem(
            HypothesisCostCategory.ProjectCost,
            CostItemKind.Other,
            "Marketing Reserve",
            displaySequence: 99);
        userRow.SetAmounts(500_000m);

        var input = new LandBuildingSummary
        {
            TotalArea = 200m,                      // FSD C01
            EstSalesPeriod = 1,                    // FSD C16
            ContingencyPercent = 0m,               // FSD C35
            ProjectContingencyPercent = 10m,       // FSD C61
            DiscountRate = 0m                      // FSD C78
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input);
        var s = result.Summary;

        Assert.Equal(50_000m, s.ProjectContingencyAmount);    // FSD C62
        Assert.Equal(550_000m, s.TotalProjectCost);           // FSD C64

        var stamped = analysis.CostItems.Single(i =>
            i.Category == HypothesisCostCategory.ProjectCost
            && i.Kind == CostItemKind.Other);
        Assert.NotNull(stamped.CategoryRatio);
        Assert.Equal(
            Math.Round(500_000m * 100m / 550_000m, 4),
            Math.Round(stamped.CategoryRatio!.Value, 4));
    }

    // ── L&B: CategoryRatio is 0 when C38 is 0 (no inputs at all) ──────────────

    [Fact]
    public void LandBuilding_UserAddedRow_CategoryRatioIsZeroWhenC38IsZero()
    {
        // A user-added row with amount = 0 produces C38 = 0 → ratio must be 0, not NaN/Infinity.
        var analysis = CreateLandBuildingAnalysis();
        var rows = new[] { MakeLbRow("M1", 50m, 1_000_000m) };

        var userRow = analysis.AddCostItem(
            HypothesisCostCategory.ProjectDevCost,
            CostItemKind.Other,
            "Empty Row",
            displaySequence: 99);
        userRow.SetAmounts(0m);

        var input = new LandBuildingSummary
        {
            TotalArea = 200m,
            EstSalesPeriod = 1,
            ContingencyPercent = 0m,
            ProjectContingencyPercent = 0m,
            DiscountRate = 0m
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input);

        Assert.Equal(0m, result.Summary.TotalProjectDevCost);

        var stamped = analysis.CostItems.Single(i =>
            i.Category == HypothesisCostCategory.ProjectDevCost
            && i.Kind == CostItemKind.Other);
        Assert.Equal(0m, stamped.CategoryRatio);
    }

    // ── Condo: Zero discount rate ─────────────────────────────────────────────

    [Fact]
    public void Condominium_ZeroDiscountRate_E54EqualsE13MinusE53_E56EqualsOne()
    {
        var analysis = CreateCondoAnalysis();
        var rows = new[]
        {
            MakeCondoRow(50m, 3_000_000m),
            MakeCondoRow(50m, 3_000_000m)
        };

        var input = new CondominiumSummary
        {
            AreaTitleDeed = 200m,                    // FSD E01
            FAR = 5m,                                // FSD E03
            TotalBuildingArea = 5000m,               // FSD E05
            EstSalesDurationMonths = 12,             // FSD E14
            CondoBuildingCostPerSqM = 20_000m,       // FSD E15
            HardCostContingencyPercent = 0m,         // FSD E25
            EstConstructionPeriodMonths = 12,        // FSD E28
            TransferFeePercent = 0m,                 // FSD E46
            DiscountRate = 0m                        // FSD E55
        };

        var result = Sut.ComputeCondominium(analysis, rows, input);

        Assert.Equal(0m, result.DiscountRate);           // FSD E55
        Assert.Equal(1m, result.DiscountRateFactor);     // FSD E56

        var expectedE54 = result.TotalRevenue!.Value - result.TotalDevCosts!.Value;
        Assert.Equal(expectedE54, result.TotalRemainingValue);   // FSD E54
        Assert.Equal(expectedE54, result.FinalRemainingValue);   // FSD E57
    }

    [Fact]
    public void Condominium_ZeroDiscountRate_E58AndE59AreRounded()
    {
        var analysis = CreateCondoAnalysis();
        var rows = new[] { MakeCondoRow(100m, 5_125_000m) };

        var input = new CondominiumSummary
        {
            AreaTitleDeed = 100m,                    // FSD E01
            FAR = 10m,                               // FSD E03
            TotalBuildingArea = 1000m,               // FSD E05
            EstSalesDurationMonths = 1,              // FSD E14
            CondoBuildingCostPerSqM = 0m,            // FSD E15
            HardCostContingencyPercent = 0m,         // FSD E25
            EstConstructionPeriodMonths = 0,         // FSD E28
            TransferFeePercent = 0m,                 // FSD E46
            DiscountRate = 0m                        // FSD E55
        };

        var result = Sut.ComputeCondominium(analysis, rows, input);

        Assert.Equal(5_130_000m, result.TotalAssetValueRounded); // FSD E58
        Assert.Equal(5_100m, result.TotalAssetValuePerSqM);      // FSD E59
    }

    // ── Condo: Non-zero discount rate (standard PV) ───────────────────────────

    [Fact]
    public void Condominium_NonZeroDiscountRate_E54IsResidual_E56IsStandardPV()
    {
        // FSD E55 = 12 (12%), E14 = 12 months
        // E54 = E13 - E53 (no discount applied — discount handled via E56)
        // E56 (standard PV) = 1 / (1.12)^(12/12) = 1 / 1.12 ≈ 0.8929
        var analysis = CreateCondoAnalysis();
        var rows = new[] { MakeCondoRow(60m, 2_000_000m) };

        var input = new CondominiumSummary
        {
            AreaTitleDeed = 50m,                     // FSD E01
            FAR = 5m,                                // FSD E03
            TotalBuildingArea = 500m,                // FSD E05
            EstSalesDurationMonths = 12,             // FSD E14
            CondoBuildingCostPerSqM = 0m,            // FSD E15
            HardCostContingencyPercent = 0m,         // FSD E25
            EstConstructionPeriodMonths = 0,         // FSD E28
            TransferFeePercent = 0m,                 // FSD E46
            DiscountRate = 12m                       // FSD E55
        };

        var result = Sut.ComputeCondominium(analysis, rows, input);

        decimal expectedE54 = result.TotalRevenue!.Value - result.TotalDevCosts!.Value;
        Assert.Equal(expectedE54, result.TotalRemainingValue); // FSD E54

        // Standard PV: 1 / (1.12)^(12/12) = 1/1.12 ≈ 0.8929
        double expectedE56 = 1.0 / Math.Pow(1.12, 1.0);
        Assert.Equal(Math.Round((decimal)expectedE56, 6), Math.Round(result.DiscountRateFactor!.Value, 6));
        Assert.True(result.DiscountRateFactor < 1m);
    }

    // ── Condo: Hard cost calculation ──────────────────────────────────────────

    [Fact]
    public void Condominium_HardCost_E17IsBuildingCostPerSqMTimesTotalArea()
    {
        var analysis = CreateCondoAnalysis();
        var rows = new[] { MakeCondoRow(50m, 1_000_000m) };

        var input = new CondominiumSummary
        {
            AreaTitleDeed = 100m,                    // FSD E01
            FAR = 5m,                                // FSD E03
            TotalBuildingArea = 800m,                // FSD E05
            EstSalesDurationMonths = 6,              // FSD E14
            CondoBuildingCostPerSqM = 25_000m,       // FSD E15
            HardCostContingencyPercent = 5m,         // FSD E25
            EstConstructionPeriodMonths = 6,         // FSD E28
            TransferFeePercent = 0m,                 // FSD E46
            DiscountRate = 0m                        // FSD E55
        };

        var result = Sut.ComputeCondominium(analysis, rows, input);

        Assert.Equal(20_000_000m, result.CondoBuildingCostTotal); // FSD E17
    }

    // ── Condo: Revenue aggregation from rows ──────────────────────────────────

    [Fact]
    public void Condominium_TotalRevenue_SummedFromAllRows()
    {
        var analysis = CreateCondoAnalysis();
        var rows = new[]
        {
            MakeCondoRow(40m, 1_500_000m),
            MakeCondoRow(55m, 2_000_000m),
            MakeCondoRow(70m, 2_500_000m)
        };

        var input = new CondominiumSummary
        {
            AreaTitleDeed = 200m,                    // FSD E01
            FAR = 5m,                                // FSD E03
            TotalBuildingArea = 5000m,               // FSD E05
            EstSalesDurationMonths = 6,              // FSD E14
            CondoBuildingCostPerSqM = 0m,            // FSD E15
            HardCostContingencyPercent = 0m,         // FSD E25
            EstConstructionPeriodMonths = 0,         // FSD E28
            TransferFeePercent = 0m,                 // FSD E46
            DiscountRate = 0m                        // FSD E55
        };

        var result = Sut.ComputeCondominium(analysis, rows, input);

        Assert.Equal(6_000_000m, result.TotalProjectSellingPrice); // FSD E12
        Assert.Equal(6_000_000m, result.TotalRevenue);             // FSD E13
    }

    // ── Condo: Sales area comes from rows ─────────────────────────────────────

    [Fact]
    public void Condominium_E09IndoorSalesArea_SummedFromRowUsableAreas()
    {
        var analysis = CreateCondoAnalysis();
        var rows = new[]
        {
            MakeCondoRow(30m, 500_000m),
            MakeCondoRow(40m, 700_000m),
            MakeCondoRow(50m, 900_000m)
        };

        var input = new CondominiumSummary
        {
            AreaTitleDeed = 100m,                    // FSD E01
            FAR = 5m,                                // FSD E03
            TotalBuildingArea = 1000m,               // FSD E05
            EstSalesDurationMonths = 3,              // FSD E14
            CondoBuildingCostPerSqM = 0m,            // FSD E15
            HardCostContingencyPercent = 0m,         // FSD E25
            EstConstructionPeriodMonths = 0,         // FSD E28
            TransferFeePercent = 0m,                 // FSD E46
            DiscountRate = 0m                        // FSD E55
        };

        var result = Sut.ComputeCondominium(analysis, rows, input);

        Assert.Equal(120m, result.IndoorSalesArea);    // FSD E09
        Assert.Equal(120m, result.ProjectSalesArea);   // FSD E10
    }

    // ── Condo: E21 fallback mirrors E18 (C-5 fix) ────────────────────────────

    [Fact]
    public void Condominium_E21FallbackMirrorsE18_WhenNoRows()
    {
        // With no rows uploaded, d03=0. FSD E18 and E21 should both fall back to SetAvgRoomSizeUnits.
        var analysis = CreateCondoAnalysis();
        var rows = new List<CondominiumUnitRow>();

        var input = new CondominiumSummary
        {
            AreaTitleDeed = 100m,                    // FSD E01
            FAR = 5m,                                // FSD E03
            TotalBuildingArea = 1000m,               // FSD E05
            EstSalesDurationMonths = 6,              // FSD E14
            CondoBuildingCostPerSqM = 0m,            // FSD E15
            SetAvgRoomSizeUnits = 50,                // FSD E18 — manual fallback
            FurniturePerUnit = 10_000m,              // FSD E20
            HardCostContingencyPercent = 0m,         // FSD E25
            EstConstructionPeriodMonths = 0,         // FSD E28
            TransferFeePercent = 0m,                 // FSD E46
            DiscountRate = 0m                        // FSD E55
        };

        var result = Sut.ComputeCondominium(analysis, rows, input);

        // FSD E18 and E21 should both be 50 (the manual fallback)
        Assert.Equal(50, result.SetAvgRoomSizeUnits);    // FSD E18
        Assert.Equal(50, result.FurnitureQuantity);      // FSD E21
        // FSD E22 = E20 * E21 = 10,000 * 50 = 500,000
        Assert.Equal(500_000m, result.FurnitureTotal);   // FSD E22
    }

    // ── L&B: Empty rows ───────────────────────────────────────────────────────

    [Fact]
    public void LandBuilding_EmptyRows_ProducesZeroRevenue_NoExceptions()
    {
        var analysis = CreateLandBuildingAnalysis();
        var rows = new List<LandBuildingUnitRow>();

        var input = new LandBuildingSummary
        {
            TotalArea = 1000m,               // FSD C01
            EstSalesPeriod = 1,              // FSD C16
            ContingencyPercent = 3m,         // FSD C35
            ProjectContingencyPercent = 3m,  // FSD C61
            DiscountRate = 0m                // FSD C78
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input);

        Assert.Equal(0m, result.Summary.TotalRevenue);               // FSD C15
        Assert.Equal(0, result.Summary.TotalUnits);                  // FSD C17
        Assert.Equal(0m, result.Summary.TotalAssetValueRounded);     // FSD C81
    }

    // ── Condo: Empty rows ─────────────────────────────────────────────────────

    [Fact]
    public void Condominium_EmptyRows_ProducesZeroRevenue_NoExceptions()
    {
        var analysis = CreateCondoAnalysis();
        var rows = new List<CondominiumUnitRow>();

        var input = new CondominiumSummary
        {
            AreaTitleDeed = 500m,                    // FSD E01
            TotalBuildingArea = 5000m,               // FSD E05
            EstSalesDurationMonths = 6,              // FSD E14
            CondoBuildingCostPerSqM = 0m,            // FSD E15
            HardCostContingencyPercent = 0m,         // FSD E25
            EstConstructionPeriodMonths = 0,         // FSD E28
            TransferFeePercent = 0m,                 // FSD E46
            DiscountRate = 0m                        // FSD E55
        };

        var result = Sut.ComputeCondominium(analysis, rows, input);

        Assert.Equal(0m, result.TotalProjectSellingPrice); // FSD E12
        Assert.Equal(0m, result.TotalAssetValueRounded);   // FSD E58
    }

    // ── SetAmounts guard (M-7) ─────────────────────────────────────────────────

    [Fact]
    public void CostItem_SetAmounts_ThrowsOnNegativeAmount()
    {
        var analysis = CreateLandBuildingAnalysis();
        var item = analysis.AddCostItem(
            HypothesisCostCategory.ProjectCost,
            CostItemKind.AllocationPermitFee,
            "Test Fee", 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => item.SetAmounts(-1m));
    }

    [Fact]
    public void CostItem_SetAmounts_AcceptsZeroAndPositive()
    {
        var analysis = CreateLandBuildingAnalysis();
        var item = analysis.AddCostItem(
            HypothesisCostCategory.ProjectCost,
            CostItemKind.AllocationPermitFee,
            "Test Fee", 1);

        var ex = Record.Exception(() => item.SetAmounts(0m));
        Assert.Null(ex);

        ex = Record.Exception(() => item.SetAmounts(100_000m));
        Assert.Null(ex);
    }

    // ── CostOfBuilding B-fields (FSD §2.1.3.5.1 Figure 52) ───────────────────

    /// <summary>
    /// Two models, two rows each.
    /// Asserts B03/B06/B07/B08 per row and B09/B10/B11 per model.
    /// Also verifies B06 is capped at 100% (30 yr × 5%/yr = 150 → 100).
    /// </summary>
    [Fact]
    public void CostOfBuilding_BFields_ComputedCorrectly_TwoModels()
    {
        var analysis = CreateLandBuildingAnalysis();

        // Model "Alpha": two rows
        //   Row 1: area=100, price=10000, year=10, annual%=5 → B03=1_000_000, B06=50, B07=500_000, B08=500_000
        //   Row 2: area=50,  price=8000,  year=30, annual%=5 → B06=min(100,150)=100, B03=400_000, B07=400_000, B08=0
        var alpha1 = analysis.AddCostItem(HypothesisCostCategory.CostOfBuilding,
            CostItemKind.BuildingConstruction, "Alpha Row 1", 1, "Alpha");
        alpha1.SetAmounts(0m);
        alpha1.SetBuildingCostInputs(100m, 10_000m, 10, 5m);

        var alpha2 = analysis.AddCostItem(HypothesisCostCategory.CostOfBuilding,
            CostItemKind.BuildingConstruction, "Alpha Row 2", 2, "Alpha");
        alpha2.SetAmounts(0m);
        alpha2.SetBuildingCostInputs(50m, 8_000m, 30, 5m);

        // Model "Beta": two rows
        //   Row 1: area=200, price=5000, year=2, annual%=10 → B03=1_000_000, B06=20, B07=200_000, B08=800_000
        //   Row 2: area=80,  price=12000, year=0, annual%=3  → B03=960_000, B06=0, B07=0, B08=960_000
        var beta1 = analysis.AddCostItem(HypothesisCostCategory.CostOfBuilding,
            CostItemKind.BuildingConstruction, "Beta Row 1", 1, "Beta");
        beta1.SetAmounts(0m);
        beta1.SetBuildingCostInputs(200m, 5_000m, 2, 10m);

        var beta2 = analysis.AddCostItem(HypothesisCostCategory.CostOfBuilding,
            CostItemKind.BuildingConstruction, "Beta Row 2", 2, "Beta");
        beta2.SetAmounts(0m);
        beta2.SetBuildingCostInputs(80m, 12_000m, 0, 3m);

        // One LB row per model so AggregateModels builds both model entries
        var rows = new[]
        {
            MakeLbRow("Alpha", 100m, 2_000_000m),
            MakeLbRow("Beta",  150m, 2_500_000m)
        };

        var input = new LandBuildingSummary
        {
            TotalArea = 500m,
            EstSalesPeriod = 1,
            ContingencyPercent = 0m,
            ProjectContingencyPercent = 0m,
            DiscountRate = 0m
        };

        var result = Sut.ComputeLandBuilding(analysis.CostItems, rows, input);

        // ── Per-row B-field assertions ──────────────────────────────────────

        // Alpha Row 1: B03=100×10000=1_000_000; B06=10×5=50; B07=1_000_000×50/100=500_000; B08=500_000
        Assert.Equal(1_000_000m, alpha1.PriceBeforeDepreciation);   // B03
        Assert.Equal(50m, alpha1.TotalDepreciationPercent);          // B06
        Assert.Equal(500_000m, alpha1.DepreciationAmount);           // B07
        Assert.Equal(500_000m, alpha1.ValueAfterDepreciation);       // B08

        // Alpha Row 2: B03=50×8000=400_000; B06=min(100,150)=100; B07=400_000; B08=0
        Assert.Equal(400_000m, alpha2.PriceBeforeDepreciation);     // B03
        Assert.Equal(100m, alpha2.TotalDepreciationPercent);         // B06 — capped at 100
        Assert.Equal(400_000m, alpha2.DepreciationAmount);           // B07
        Assert.Equal(0m, alpha2.ValueAfterDepreciation);             // B08

        // Beta Row 1: B03=200×5000=1_000_000; B06=2×10=20; B07=200_000; B08=800_000
        Assert.Equal(1_000_000m, beta1.PriceBeforeDepreciation);    // B03
        Assert.Equal(20m, beta1.TotalDepreciationPercent);           // B06
        Assert.Equal(200_000m, beta1.DepreciationAmount);            // B07
        Assert.Equal(800_000m, beta1.ValueAfterDepreciation);        // B08

        // Beta Row 2: B03=80×12000=960_000; B06=0×3=0; B07=0; B08=960_000
        Assert.Equal(960_000m, beta2.PriceBeforeDepreciation);      // B03
        Assert.Equal(0m, beta2.TotalDepreciationPercent);            // B06
        Assert.Equal(0m, beta2.DepreciationAmount);                  // B07
        Assert.Equal(960_000m, beta2.ValueAfterDepreciation);        // B08

        // ── Per-model B09/B10/B11 assertions ───────────────────────────────

        var alphaAgg = result.Models["Alpha"];
        // B09 = 100+50 = 150
        Assert.Equal(150m, alphaAgg.TotalBuildingAreaSqM);
        // B10 = 1_000_000+400_000 = 1_400_000
        Assert.Equal(1_400_000m, alphaAgg.TotalPriceBeforeDepreciation);
        // B11 = 500_000+0 = 500_000
        Assert.Equal(500_000m, alphaAgg.TotalBuildingValueAfterDepreciation);

        var betaAgg = result.Models["Beta"];
        // B09 = 200+80 = 280
        Assert.Equal(280m, betaAgg.TotalBuildingAreaSqM);
        // B10 = 1_000_000+960_000 = 1_960_000
        Assert.Equal(1_960_000m, betaAgg.TotalPriceBeforeDepreciation);
        // B11 = 800_000+960_000 = 1_760_000
        Assert.Equal(1_760_000m, betaAgg.TotalBuildingValueAfterDepreciation);

        // ── C19 uses B11 as source → TotalValueAfterDepreciation ───────────
        Assert.Equal(500_000m, alphaAgg.TotalValueAfterDepreciation);   // = B11 Alpha
        Assert.Equal(1_760_000m, betaAgg.TotalValueAfterDepreciation);  // = B11 Beta
    }

    [Fact]
    public void CostOfBuilding_NullInputs_ComputedFieldsAreNull()
    {
        // A newly-added row with no inputs yet should have all computed fields null.
        var analysis = CreateLandBuildingAnalysis();
        var item = analysis.AddCostItem(HypothesisCostCategory.CostOfBuilding,
            CostItemKind.BuildingConstruction, "Empty Row", 1, "ModelX");
        item.SetAmounts(0m);
        // No SetBuildingCostInputs call — all inputs remain null

        var rows = new[] { MakeLbRow("ModelX", 100m, 1_000_000m) };
        var input = new LandBuildingSummary
        {
            TotalArea = 200m,
            EstSalesPeriod = 1,
            ContingencyPercent = 0m,
            ProjectContingencyPercent = 0m,
            DiscountRate = 0m
        };

        Sut.ComputeLandBuilding(analysis.CostItems, rows, input);

        // All B03/B06/B07/B08 should remain null because all inputs were null
        Assert.Null(item.PriceBeforeDepreciation);
        Assert.Null(item.TotalDepreciationPercent);
        Assert.Null(item.DepreciationAmount);
        Assert.Null(item.ValueAfterDepreciation);
    }

    // ── CostOfBuilding: Period depreciation method ────────────────────────────

    /// <summary>
    /// Two periods: {1→5, 3.0%/yr} and {6→10, 2.5%/yr}.
    ///   Period 1 contributes: (5 - 1 + 1) × 3.0 = 15.0
    ///   Period 2 contributes: (10 - 6 + 1) × 2.5 = 12.5
    ///   B06 = 15.0 + 12.5 = 27.5
    ///   Row: Area=200, Price=10000 → B03=2_000_000; B07=550_000; B08=1_450_000
    /// </summary>
    [Fact]
    public void CostOfBuilding_PeriodMethod_TotalDepUsesPeriodsSum()
    {
        var analysis = CreateLandBuildingAnalysis();

        var item = analysis.AddCostItem(HypothesisCostCategory.CostOfBuilding,
            CostItemKind.BuildingConstruction, "Period Row", 1, "M1");
        item.SetAmounts(0m);
        item.SetBuildingCostInputs(
            area: 200m,
            pricePerSqM: 10_000m,
            year: null,
            annualDepreciationPercent: null,
            isBuilding: true,
            depreciationMethod: DepreciationMethod.Period,
            depreciationPeriods:
            [
                (AtYear: 1, ToYear: 5, DepreciationPerYear: 3.0m),
                (AtYear: 6, ToYear: 10, DepreciationPerYear: 2.5m)
            ]);

        var rows = new[] { MakeLbRow("M1", 100m, 5_000_000m) };
        var input = new LandBuildingSummary
        {
            TotalArea = 500m,
            EstSalesPeriod = 1,
            ContingencyPercent = 0m,
            ProjectContingencyPercent = 0m,
            DiscountRate = 0m
        };

        Sut.ComputeLandBuilding(analysis.CostItems, rows, input);

        // B03 = 200 × 10_000 = 2_000_000
        Assert.Equal(2_000_000m, item.PriceBeforeDepreciation);   // B03
        // B06 = (5-1+1)×3.0 + (10-6+1)×2.5 = 15.0 + 12.5 = 27.5
        Assert.Equal(27.5m, item.TotalDepreciationPercent);       // B06
        // B07 = 2_000_000 × 27.5 / 100 = 550_000
        Assert.Equal(550_000m, item.DepreciationAmount);          // B07
        // B08 = 2_000_000 − 550_000 = 1_450_000
        Assert.Equal(1_450_000m, item.ValueAfterDepreciation);    // B08
    }

    /// <summary>
    /// Periods whose sum exceeds 100 are clamped.
    ///   Single period: {0→100, 2%/yr} → sum = 101 × 2 = 202 → clamped to 100.
    /// </summary>
    [Fact]
    public void CostOfBuilding_PeriodMethod_CapsAt100()
    {
        var analysis = CreateLandBuildingAnalysis();

        var item = analysis.AddCostItem(HypothesisCostCategory.CostOfBuilding,
            CostItemKind.BuildingConstruction, "High Dep Row", 1, "M1");
        item.SetAmounts(0m);
        item.SetBuildingCostInputs(
            area: 100m,
            pricePerSqM: 5_000m,
            year: null,
            annualDepreciationPercent: null,
            isBuilding: true,
            depreciationMethod: DepreciationMethod.Period,
            depreciationPeriods:
            [
                (AtYear: 0, ToYear: 100, DepreciationPerYear: 2m)   // sum = 101 × 2 = 202
            ]);

        var rows = new[] { MakeLbRow("M1", 100m, 1_000_000m) };
        var input = new LandBuildingSummary
        {
            TotalArea = 200m,
            EstSalesPeriod = 1,
            ContingencyPercent = 0m,
            ProjectContingencyPercent = 0m,
            DiscountRate = 0m
        };

        Sut.ComputeLandBuilding(analysis.CostItems, rows, input);

        // B06 should be capped at 100 despite sum=202
        Assert.Equal(100m, item.TotalDepreciationPercent);   // B06 capped
        // B03 = 100 × 5000 = 500_000; B07 = 500_000; B08 = 0
        Assert.Equal(500_000m, item.PriceBeforeDepreciation);
        Assert.Equal(500_000m, item.DepreciationAmount);
        Assert.Equal(0m, item.ValueAfterDepreciation);
    }

    /// <summary>
    /// Pre-existing Gross method behavior must remain unchanged after the Period feature.
    ///   Row: area=100, price=10000, year=10, annual%=5 → B06 = min(100, 10×5) = 50.
    /// </summary>
    [Fact]
    public void CostOfBuilding_GrossMethod_StillUsesYearTimesAnnual()
    {
        var analysis = CreateLandBuildingAnalysis();

        var item = analysis.AddCostItem(HypothesisCostCategory.CostOfBuilding,
            CostItemKind.BuildingConstruction, "Gross Row", 1, "M1");
        item.SetAmounts(0m);
        item.SetBuildingCostInputs(
            area: 100m,
            pricePerSqM: 10_000m,
            year: 10,
            annualDepreciationPercent: 5m,
            isBuilding: true,
            depreciationMethod: DepreciationMethod.Gross);

        var rows = new[] { MakeLbRow("M1", 100m, 2_000_000m) };
        var input = new LandBuildingSummary
        {
            TotalArea = 200m,
            EstSalesPeriod = 1,
            ContingencyPercent = 0m,
            ProjectContingencyPercent = 0m,
            DiscountRate = 0m
        };

        Sut.ComputeLandBuilding(analysis.CostItems, rows, input);

        // B03 = 100 × 10_000 = 1_000_000; B06 = min(100, 10×5) = 50; B07 = 500_000; B08 = 500_000
        Assert.Equal(1_000_000m, item.PriceBeforeDepreciation);   // B03
        Assert.Equal(50m, item.TotalDepreciationPercent);         // B06
        Assert.Equal(500_000m, item.DepreciationAmount);          // B07
        Assert.Equal(500_000m, item.ValueAfterDepreciation);      // B08
    }

    // ── L&B: Land area derived from titles (C01/C02/C10/C10A) ───────────────

    [Fact]
    public void LandArea_C01_PrefersTitleSum_OverInput()
    {
        // When titleSum is provided it must win over input.TotalArea.
        var analysis = CreateLandBuildingAnalysis();
        var rows = new[] { MakeLbRow("M1", 50m, 1_000_000m) };

        var input = new LandBuildingSummary
        {
            TotalArea = 999m,            // FSD C01 legacy — should be IGNORED
            EstSalesPeriod = 1,          // FSD C16
            ContingencyPercent = 0m,     // FSD C35
            ProjectContingencyPercent = 0m, // FSD C61
            DiscountRate = 0m            // FSD C78
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input, totalLandAreaFromTitles: 300m);

        Assert.Equal(300m, result.Summary.TotalArea); // FSD C01 = titleSum
    }

    [Fact]
    public void LandArea_C01_FallsBackToInput_WhenTitleSumNull()
    {
        // When titleSum is null the legacy input.TotalArea must be used as C01.
        var analysis = CreateLandBuildingAnalysis();
        var rows = new[] { MakeLbRow("M1", 50m, 1_000_000m) };

        var input = new LandBuildingSummary
        {
            TotalArea = 250m,            // FSD C01 — used when no title sum
            EstSalesPeriod = 1,
            ContingencyPercent = 0m,
            ProjectContingencyPercent = 0m,
            DiscountRate = 0m
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input, totalLandAreaFromTitles: null);

        Assert.Equal(250m, result.Summary.TotalArea); // FSD C01 = input fallback
    }

    [Fact]
    public void LandArea_C02_IsDerived_FromC03DivC01()
    {
        // titleSum=250, model row landArea=200 → C03=200, C02=200/250*100=80%.
        var analysis = CreateLandBuildingAnalysis();
        var rows = new[] { MakeLbRow("M1", 200m, 2_000_000m) };

        var input = new LandBuildingSummary
        {
            TotalArea = 999m,            // ignored — title sum takes over
            SellingAreaPercent = 50m,    // ignored — derived
            EstSalesPeriod = 1,
            ContingencyPercent = 0m,
            ProjectContingencyPercent = 0m,
            DiscountRate = 0m
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input, totalLandAreaFromTitles: 250m);
        var s = result.Summary;

        Assert.Equal(200m, s.SellingArea);           // FSD C03
        Assert.Equal(80m, s.SellingAreaPercent);     // FSD C02 = 200/250*100
    }

    [Fact]
    public void LandArea_C10A_IsDerived_AsC01MinusC03()
    {
        // titleSum=250, C03=200 → C10A=50, C10=50/250*100=20%.
        var analysis = CreateLandBuildingAnalysis();
        var rows = new[] { MakeLbRow("M1", 200m, 2_000_000m) };

        var input = new LandBuildingSummary
        {
            TotalArea = 999m,
            PublicUtilityAreaPercent = 99m,  // ignored — derived
            EstSalesPeriod = 1,
            ContingencyPercent = 0m,
            ProjectContingencyPercent = 0m,
            DiscountRate = 0m
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input, totalLandAreaFromTitles: 250m);
        var s = result.Summary;

        Assert.Equal(50m, s.PublicUtilityArea);          // FSD C10A = 250-200
        Assert.Equal(20m, s.PublicUtilityAreaPercent);   // FSD C10  = 50/250*100
    }

    [Fact]
    public void LandArea_C10A_FloorsAtZero_WhenC03ExceedsC01()
    {
        // Model totals > title sum (data inconsistency) → C10A clamped to 0, C10 = 0.
        var analysis = CreateLandBuildingAnalysis();
        // Two rows: total land area = 200+150 = 350, but titleSum = 300
        var rows = new[]
        {
            MakeLbRow("M1", 200m, 1_000_000m),
            MakeLbRow("M1", 150m, 1_000_000m)
        };

        var input = new LandBuildingSummary
        {
            TotalArea = 999m,
            EstSalesPeriod = 1,
            ContingencyPercent = 0m,
            ProjectContingencyPercent = 0m,
            DiscountRate = 0m
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input, totalLandAreaFromTitles: 300m);
        var s = result.Summary;

        Assert.Equal(350m, s.SellingArea);          // FSD C03 = 350 (from rows)
        Assert.Equal(0m, s.PublicUtilityArea);      // FSD C10A = max(0, 300-350) = 0
        Assert.Equal(0m, s.PublicUtilityAreaPercent); // FSD C10 = 0
    }
}
