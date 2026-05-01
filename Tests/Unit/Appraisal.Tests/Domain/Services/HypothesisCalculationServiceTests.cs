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
///   - C77: if C78=0 → C15-C76; else (C15-C76)*C78
///   - C79: if C78=0 → 1; else PV factor = 1/(1+C78/100)^(C18/12)
///   - C81: Round(C80, 10000)
///   - C82: Round(C81/C01, 100)
///   - E54: mirrors C77 conditional with E55
///   - E58: Round(E57, 10000)
///   - E59: Round(E58/E05, 100)
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
        => LandBuildingUnitRow.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "P1", "H1", model, landArea, price);

    private static CondominiumUnitRow MakeCondoRow(decimal usableArea, decimal price)
        => CondominiumUnitRow.Create(Guid.NewGuid(), Guid.NewGuid(), 1, 1, "A", "1A", "Studio", usableArea, price);

    // ── RoundToNearest (via C81/E58 output) ──────────────────────────────────

    [Theory]
    [InlineData(123456, 10000, 120000)]   // round down
    [InlineData(125000, 10000, 130000)]   // midpoint rounds up (AwayFromZero)
    [InlineData(127000, 10000, 130000)]   // round up
    [InlineData(0, 10000, 0)]             // zero
    public void RoundToNearest_10000_ProducesExpectedC81(decimal raw, decimal nearest, decimal expected)
    {
        // Arrange — configure input so that C80 = raw exactly:
        //   C15 = revenue, C76 = devCosts, C78 = 0 → C77 = C15-C76 = raw, C79 = 1, C80 = raw
        var analysis = CreateLandBuildingAnalysis();
        var rows = new List<LandBuildingUnitRow>
        {
            MakeLbRow("M1", 50m, raw) // selling price = raw → C15 = raw
        };
        // cost items = 0 → C76 = 0 → C77 = raw - 0 = raw

        var input = new LandBuildingSummary
        {
            C01TotalArea = 1000m,
            C16EstSalesPeriod = 1,
            C78DiscountRate = 0m
        };

        // Act
        var result = Sut.ComputeLandBuilding(analysis, rows, input);

        // Assert
        Assert.Equal(expected, result.Summary.C81TotalAssetValueRounded);
        _ = nearest; // unused — nearest is implicit (10000)
    }

    // ── L&B: Zero discount rate ───────────────────────────────────────────────

    [Fact]
    public void LandBuilding_ZeroDiscountRate_C77EqualsC15MinusC76_C79EqualsOne()
    {
        // C78 = 0 → C77 = C15 - C76; C79 = 1; C80 = C77
        var analysis = CreateLandBuildingAnalysis();

        // Two units: selling price 1,000,000 each → C15 = 2,000,000
        var rows = new[]
        {
            MakeLbRow("TypeA", 100m, 1_000_000m),
            MakeLbRow("TypeA", 100m, 1_000_000m)
        };

        // Cost item: building cost per unit = 300,000 → total = 300,000 × 2 = 600,000
        // C38 = 600,000 + 0 + 0 + contingency(3%) = 618,000
        // C64 = 0 (no project cost items or selling/adv)
        // C72 = 0 (no gov tax percents) → C76 = C38 = 618,000
        var costItem = analysis.AddCostItem(HypothesisCostCategory.CostOfBuilding, "Construction", 1, "TypeA");
        costItem.SetAmounts(300_000m, 0m, 0, 0m);

        var input = new LandBuildingSummary
        {
            C01TotalArea = 500m,
            C16EstSalesPeriod = 1,
            C35ContingencyPercent = 3m,
            C61ProjectContingencyPercent = 3m,
            C78DiscountRate = 0m
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input);
        var s = result.Summary;

        Assert.Equal(0m, s.C78DiscountRate);
        Assert.Equal(1m, s.C79DiscountRateFactor);

        decimal expectedC15 = 2_000_000m;
        Assert.Equal(expectedC15, s.C15TotalRevenue);

        // C77 = C15 - C76
        var expectedC77 = s.C15TotalRevenue!.Value - s.C76TotalDevCostsAndExpenses!.Value;
        Assert.Equal(expectedC77, s.C77CurrentPropertyValue);

        // C80 = C77 × C79 = C77 × 1 = C77
        Assert.Equal(expectedC77, s.C80FinalPropertyValue);
    }

    [Fact]
    public void LandBuilding_ZeroDiscountRate_C81AndC82AreRounded()
    {
        var analysis = CreateLandBuildingAnalysis();
        // Revenue = 5,125,000; all other costs = 0 → C77 = C80 = 5,125,000
        var rows = new[] { MakeLbRow("T1", 100m, 5_125_000m) };

        var input = new LandBuildingSummary
        {
            C01TotalArea = 100m, // C82 = C81 / 100
            C16EstSalesPeriod = 1,
            C35ContingencyPercent = 0m,
            C61ProjectContingencyPercent = 0m,
            C78DiscountRate = 0m
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input);
        var s = result.Summary;

        // C81 = Round(5,125,000; 10000) = 5,130,000 (midpoint rounds up)
        Assert.Equal(5_130_000m, s.C81TotalAssetValueRounded);
        // C82 = Round(5,130,000 / 100; 100) = Round(51300; 100) = 51300
        Assert.Equal(51_300m, s.C82TotalAssetValuePerSqWa);
    }

    // ── L&B: Non-zero discount rate ───────────────────────────────────────────

    [Fact]
    public void LandBuilding_NonZeroDiscountRate_C77UsesFactor_C79IsCorrectPV()
    {
        // C78 = 10 (10%), C18 = 12 months
        // C79 = 1 / (1 + 10/100)^(12/12) = 1/1.1 ≈ 0.909090...
        // C77 = (C15 - C76) * C78 = residual * 10
        var analysis = CreateLandBuildingAnalysis();
        var rows = new[] { MakeLbRow("M1", 50m, 1_000_000m) }; // C15 = 1,000,000

        var input = new LandBuildingSummary
        {
            C01TotalArea = 200m,
            C16EstSalesPeriod = 1, // C18 = 1 unit / 1 per period = 1 → wait, C18 = ceil(C17/C16)
            // C17 = 1 unit, C16 = 1 → C18 = 1 period
            // But C18 is in months for the PV calc; let's use C40 for construction periods
            C35ContingencyPercent = 0m,
            C40EstConstructionPeriod = 1,
            C61ProjectContingencyPercent = 0m,
            C78DiscountRate = 10m
        };
        // C18 = ceil(C17/C16) = ceil(1/1) = 1 month (sales period)
        // C77 = (1,000,000 - 0) * 10 = 10,000,000
        // C79 = 1/(1.1)^(1/12)
        var result = Sut.ComputeLandBuilding(analysis, rows, input);
        var s = result.Summary;

        // C77 should be (C15 - C76) * 10
        decimal expectedC77 = (s.C15TotalRevenue!.Value - s.C76TotalDevCostsAndExpenses!.Value) * 10m;
        Assert.Equal(expectedC77, s.C77CurrentPropertyValue);

        // C79 must be <1 (discounting)
        Assert.True(s.C79DiscountRateFactor < 1m);

        // Expected C79 = 1/(1.1)^(1/12) — compare with 6 decimal places to allow for float→decimal cast precision
        double expectedC79 = 1.0 / Math.Pow(1.1, 1.0 / 12.0);
        Assert.Equal(Math.Round((decimal)expectedC79, 6), Math.Round(s.C79DiscountRateFactor!.Value, 6));
    }

    // ── L&B: Duration calculation (C18) ──────────────────────────────────────

    [Fact]
    public void LandBuilding_C18IsCalculatedFromC17DividedByC16_CeilingApplied()
    {
        // 5 units, sales period = 2 → C18 = ceil(5/2) = 3
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
            C01TotalArea = 1000m,
            C16EstSalesPeriod = 2,
            C35ContingencyPercent = 0m,
            C61ProjectContingencyPercent = 0m,
            C78DiscountRate = 0m
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input);

        Assert.Equal(5, result.Summary.C17TotalUnits);
        Assert.Equal(3, result.Summary.C18EstimatedDurationMonths);
    }

    // ── L&B: Cost item aggregation ────────────────────────────────────────────

    [Fact]
    public void LandBuilding_PerModelBuildingCost_AggregatedCorrectly()
    {
        var analysis = CreateLandBuildingAnalysis();

        // Model "A" has 2 units, cost per unit = 200,000
        // Model "B" has 1 unit, cost per unit = 300,000
        var rows = new[]
        {
            MakeLbRow("A", 60m, 800_000m),
            MakeLbRow("A", 60m, 800_000m),
            MakeLbRow("B", 80m, 1_000_000m)
        };

        var costA = analysis.AddCostItem(HypothesisCostCategory.CostOfBuilding, "Construction", 1, "A");
        costA.SetAmounts(200_000m, 0m, 0, 0m);

        var costB = analysis.AddCostItem(HypothesisCostCategory.CostOfBuilding, "Construction", 2, "B");
        costB.SetAmounts(300_000m, 0m, 0, 0m);

        var input = new LandBuildingSummary
        {
            C01TotalArea = 500m,
            C16EstSalesPeriod = 1,
            C35ContingencyPercent = 0m,
            C61ProjectContingencyPercent = 0m,
            C78DiscountRate = 0m
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input);

        // sumBuildingCostAllModels = 200,000*2 + 300,000*1 = 700,000
        // C38 = 700,000 (no contingency, no pub utility, no land filling)
        Assert.Equal(700_000m, result.Summary.C38TotalProjectDevCost);

        // Per-model aggregates
        Assert.Equal(2, result.Models["A"].UnitCount);
        Assert.Equal(400_000m, result.Models["A"].TotalValueAfterDepreciationAllUnits);
        Assert.Equal(1, result.Models["B"].UnitCount);
        Assert.Equal(300_000m, result.Models["B"].TotalValueAfterDepreciationAllUnits);
    }

    // ── L&B: Public utility cost ──────────────────────────────────────────────

    [Fact]
    public void LandBuilding_PublicUtilityCost_C29EqualC27TimesC01()
    {
        var analysis = CreateLandBuildingAnalysis();
        var rows = new[] { MakeLbRow("M1", 100m, 500_000m) };

        var input = new LandBuildingSummary
        {
            C01TotalArea = 400m,
            C16EstSalesPeriod = 1,
            C27PublicUtilityRatePerSqWa = 500m,
            C35ContingencyPercent = 0m,
            C61ProjectContingencyPercent = 0m,
            C78DiscountRate = 0m
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input);

        // C28 = C01 = 400; C29 = 500 * 400 = 200,000
        Assert.Equal(400m, result.Summary.C28PublicUtilityArea);
        Assert.Equal(200_000m, result.Summary.C29PublicUtilityCost);
    }

    // ── Condo: Zero discount rate ─────────────────────────────────────────────

    [Fact]
    public void Condominium_ZeroDiscountRate_E54EqualsE13MinusE53_E56EqualsOne()
    {
        // E55 = 0 → E54 = E13 - E53; E56 = 1; E57 = E54
        var analysis = CreateCondoAnalysis();
        var rows = new[]
        {
            MakeCondoRow(50m, 3_000_000m),
            MakeCondoRow(50m, 3_000_000m)
        };
        // E12 = E13 = 6,000,000 total revenue

        var input = new CondominiumSummary
        {
            E01AreaTitleDeed = 200m,
            E03FAR = 5m,
            E05TotalBuildingArea = 5000m,
            E14EstSalesDurationMonths = 12,
            E15CondoBuildingCostPerSqM = 20_000m,
            E25HardCostContingencyPercent = 0m,
            E28EstConstructionPeriodMonths = 12,
            E46TransferFeePercent = 0m,
            E55DiscountRate = 0m
        };

        var result = Sut.ComputeCondominium(analysis, rows, input);

        Assert.Equal(0m, result.E55DiscountRate);
        Assert.Equal(1m, result.E56DiscountRateFactor);

        var expectedE54 = result.E13TotalRevenue!.Value - result.E53TotalDevCosts!.Value;
        Assert.Equal(expectedE54, result.E54TotalRemainingValue);
        Assert.Equal(expectedE54, result.E57FinalRemainingValue); // E57 = E54 * 1
    }

    [Fact]
    public void Condominium_ZeroDiscountRate_E58AndE59AreRounded()
    {
        // Set up so E57 = 5,125,000 exactly
        // All costs = 0; E13 = 5,125,000 → E53 = 0 → E54 = 5,125,000
        var analysis = CreateCondoAnalysis();
        var rows = new[] { MakeCondoRow(100m, 5_125_000m) };

        var input = new CondominiumSummary
        {
            E01AreaTitleDeed = 100m,
            E03FAR = 10m,
            E05TotalBuildingArea = 1000m,
            E14EstSalesDurationMonths = 1,
            E15CondoBuildingCostPerSqM = 0m,
            E25HardCostContingencyPercent = 0m,
            E28EstConstructionPeriodMonths = 0,
            E46TransferFeePercent = 0m,
            E55DiscountRate = 0m
        };

        var result = Sut.ComputeCondominium(analysis, rows, input);

        // E58 = Round(5,125,000; 10000) = 5,130,000
        Assert.Equal(5_130_000m, result.E58TotalAssetValueRounded);
        // E59 = Round(5,130,000 / 1000; 100) = Round(5130; 100) = 5100
        Assert.Equal(5_100m, result.E59TotalAssetValuePerSqM);
    }

    // ── Condo: Non-zero discount rate ─────────────────────────────────────────

    [Fact]
    public void Condominium_NonZeroDiscountRate_E54UsesFactor_E56IsCorrectPV()
    {
        // E55 = 12, E14 = 12 months → E56 = 1/(1.12)^1 ≈ 0.8929
        // E54 = (E13 - E53) * 12
        var analysis = CreateCondoAnalysis();
        var rows = new[] { MakeCondoRow(60m, 2_000_000m) };

        var input = new CondominiumSummary
        {
            E01AreaTitleDeed = 50m,
            E03FAR = 5m,
            E05TotalBuildingArea = 500m,
            E14EstSalesDurationMonths = 12,
            E15CondoBuildingCostPerSqM = 0m,
            E25HardCostContingencyPercent = 0m,
            E28EstConstructionPeriodMonths = 0,
            E46TransferFeePercent = 0m,
            E55DiscountRate = 12m
        };

        var result = Sut.ComputeCondominium(analysis, rows, input);

        decimal residual = result.E13TotalRevenue!.Value - result.E53TotalDevCosts!.Value;
        decimal expectedE54 = residual * 12m;
        Assert.Equal(expectedE54, result.E54TotalRemainingValue);

        // E56 = 1/(1.12)^(12/12) = 1/1.12 — compare with 6 decimal places to allow for float→decimal cast precision
        double expectedE56 = 1.0 / Math.Pow(1.12, 1.0);
        Assert.Equal(Math.Round((decimal)expectedE56, 6), Math.Round(result.E56DiscountRateFactor!.Value, 6));
        Assert.True(result.E56DiscountRateFactor < 1m);
    }

    // ── Condo: Hard cost calculation ──────────────────────────────────────────

    [Fact]
    public void Condominium_HardCost_E17IsBuildingCostPerSqMTimesTotalArea()
    {
        var analysis = CreateCondoAnalysis();
        var rows = new[] { MakeCondoRow(50m, 1_000_000m) };

        var input = new CondominiumSummary
        {
            E01AreaTitleDeed = 100m,
            E03FAR = 5m,
            E05TotalBuildingArea = 800m,
            E14EstSalesDurationMonths = 6,
            E15CondoBuildingCostPerSqM = 25_000m,
            E25HardCostContingencyPercent = 5m,
            E28EstConstructionPeriodMonths = 6,
            E46TransferFeePercent = 0m,
            E55DiscountRate = 0m
        };

        var result = Sut.ComputeCondominium(analysis, rows, input);

        // E17 = E15 * E16 = 25,000 * 800 = 20,000,000
        Assert.Equal(20_000_000m, result.E17CondoBuildingCostTotal);
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
            E01AreaTitleDeed = 200m,
            E03FAR = 5m,
            E05TotalBuildingArea = 5000m,
            E14EstSalesDurationMonths = 6,
            E15CondoBuildingCostPerSqM = 0m,
            E25HardCostContingencyPercent = 0m,
            E28EstConstructionPeriodMonths = 0,
            E46TransferFeePercent = 0m,
            E55DiscountRate = 0m
        };

        var result = Sut.ComputeCondominium(analysis, rows, input);

        Assert.Equal(6_000_000m, result.E12TotalProjectSellingPrice);
        Assert.Equal(6_000_000m, result.E13TotalRevenue);
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
            E01AreaTitleDeed = 100m,
            E03FAR = 5m,
            E05TotalBuildingArea = 1000m,
            E14EstSalesDurationMonths = 3,
            E15CondoBuildingCostPerSqM = 0m,
            E25HardCostContingencyPercent = 0m,
            E28EstConstructionPeriodMonths = 0,
            E46TransferFeePercent = 0m,
            E55DiscountRate = 0m
        };

        var result = Sut.ComputeCondominium(analysis, rows, input);

        // E09 = sum(usable areas) = 30+40+50 = 120
        Assert.Equal(120m, result.E09IndoorSalesArea);
        // E10 = E09
        Assert.Equal(120m, result.E10ProjectSalesArea);
    }

    // ── L&B: Empty rows ───────────────────────────────────────────────────────

    [Fact]
    public void LandBuilding_EmptyRows_ProducesZeroRevenue_NoExceptions()
    {
        var analysis = CreateLandBuildingAnalysis();
        var rows = new List<LandBuildingUnitRow>();

        var input = new LandBuildingSummary
        {
            C01TotalArea = 1000m,
            C16EstSalesPeriod = 1,
            C35ContingencyPercent = 3m,
            C61ProjectContingencyPercent = 3m,
            C78DiscountRate = 0m
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input);

        Assert.Equal(0m, result.Summary.C15TotalRevenue);
        Assert.Equal(0, result.Summary.C17TotalUnits);
        Assert.Equal(0m, result.Summary.C81TotalAssetValueRounded);
    }

    // ── Condo: Empty rows ─────────────────────────────────────────────────────

    [Fact]
    public void Condominium_EmptyRows_ProducesZeroRevenue_NoExceptions()
    {
        var analysis = CreateCondoAnalysis();
        var rows = new List<CondominiumUnitRow>();

        var input = new CondominiumSummary
        {
            E01AreaTitleDeed = 500m,
            E05TotalBuildingArea = 5000m,
            E14EstSalesDurationMonths = 6,
            E15CondoBuildingCostPerSqM = 0m,
            E25HardCostContingencyPercent = 0m,
            E28EstConstructionPeriodMonths = 0,
            E46TransferFeePercent = 0m,
            E55DiscountRate = 0m
        };

        var result = Sut.ComputeCondominium(analysis, rows, input);

        Assert.Equal(0m, result.E12TotalProjectSellingPrice);
        Assert.Equal(0m, result.E58TotalAssetValueRounded);
    }
}
