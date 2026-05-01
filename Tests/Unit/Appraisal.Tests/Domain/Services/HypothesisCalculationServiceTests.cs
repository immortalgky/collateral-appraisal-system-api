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
///   - C77: if C78=0 → C15-C76; else (C15-C76)*(C78/100)   [percentage applied]
///   - C79: Reading 2 — 1 / (1 + (C78/100)^(C18/12))
///   - C81: Round(C80, 10000)
///   - C82: Round(C81/C01, 100)
///   - E54: mirrors C77 conditional with E55/100
///   - E56: Reading 2 — 1 / (1 + (E55/100)^(E14/12))
///   - E58: Round(E57, 10000)
///   - E59: Round(E58/E05, 100)
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
        var analysis = CreateLandBuildingAnalysis();
        var rows = new List<LandBuildingUnitRow>
        {
            MakeLbRow("M1", 50m, raw)
        };

        var input = new LandBuildingSummary
        {
            C01TotalArea = 1000m,
            C16EstSalesPeriod = 1,
            C78DiscountRate = 0m
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input);

        Assert.Equal(expected, result.Summary.C81TotalAssetValueRounded);
        _ = nearest;
    }

    // ── L&B: Zero discount rate ───────────────────────────────────────────────

    [Fact]
    public void LandBuilding_ZeroDiscountRate_C77EqualsC15MinusC76_C79EqualsOne()
    {
        // C78 = 0 → C77 = C15 - C76; C79 = 1; C80 = C77
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

        var expectedC77 = s.C15TotalRevenue!.Value - s.C76TotalDevCostsAndExpenses!.Value;
        Assert.Equal(expectedC77, s.C77CurrentPropertyValue);

        Assert.Equal(expectedC77, s.C80FinalPropertyValue);
    }

    [Fact]
    public void LandBuilding_ZeroDiscountRate_C81AndC82AreRounded()
    {
        var analysis = CreateLandBuildingAnalysis();
        var rows = new[] { MakeLbRow("T1", 100m, 5_125_000m) };

        var input = new LandBuildingSummary
        {
            C01TotalArea = 100m,
            C16EstSalesPeriod = 1,
            C35ContingencyPercent = 0m,
            C61ProjectContingencyPercent = 0m,
            C78DiscountRate = 0m
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input);
        var s = result.Summary;

        Assert.Equal(5_130_000m, s.C81TotalAssetValueRounded);
        Assert.Equal(51_300m, s.C82TotalAssetValuePerSqWa);
    }

    // ── L&B: Non-zero discount rate (Reading 2 formulas) ──────────────────────

    [Fact]
    public void LandBuilding_NonZeroDiscountRate_C77AppliesPercentageFactor_C79IsReading2()
    {
        // C78 = 10 (10%), C18 = 1 month (from 1 unit / 1 per period)
        // C77 = (C15 - C76) * (10/100) = residual * 0.10
        // C79 (Reading 2) = 1 / (1 + (10/100)^(1/12)) = 1 / (1 + 0.10^(1/12)) ≈ 0.548
        var analysis = CreateLandBuildingAnalysis();
        var rows = new[] { MakeLbRow("M1", 50m, 1_000_000m) };

        var input = new LandBuildingSummary
        {
            C01TotalArea = 200m,
            C16EstSalesPeriod = 1,
            C35ContingencyPercent = 0m,
            C40EstConstructionPeriod = 1,
            C61ProjectContingencyPercent = 0m,
            C78DiscountRate = 10m
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input);
        var s = result.Summary;

        // C18 = ceil(1/1) = 1
        Assert.Equal(1, s.C18EstimatedDurationMonths);

        // C77 = (C15 - C76) * (C78/100)
        decimal residual = s.C15TotalRevenue!.Value - s.C76TotalDevCostsAndExpenses!.Value;
        decimal expectedC77 = residual * (10m / 100m);
        Assert.Equal(expectedC77, s.C77CurrentPropertyValue);

        // C79 Reading 2: 1 / (1 + (C78/100)^(C18/12)) = 1 / (1 + (0.10)^(1/12))
        double expectedC79 = 1.0 / (1.0 + Math.Pow(0.10, 1.0 / 12.0));
        Assert.Equal(Math.Round((decimal)expectedC79, 6), Math.Round(s.C79DiscountRateFactor!.Value, 6));
        Assert.True(s.C79DiscountRateFactor < 1m);
    }

    [Fact]
    public void LandBuilding_NonZeroDiscountRate_C79Reading2_DivergenceFrom_Reading1()
    {
        // At C78=10, C18=24 the two readings differ:
        // Reading 1: 1/(1.10)^2 = 0.826
        // Reading 2: 1/(1 + 0.10^2) = 1/(1.01) = 0.990
        var analysis = CreateLandBuildingAnalysis();
        var rows = new[] { MakeLbRow("M1", 50m, 1_000_000m) };

        // To get C18=24 we need 24 units / 1 per period
        var manyRows = Enumerable.Range(1, 24).Select(_ => MakeLbRow("M1", 50m, 100_000m)).ToList();

        var input = new LandBuildingSummary
        {
            C01TotalArea = 200m,
            C16EstSalesPeriod = 1, // C18 = ceil(24/1) = 24
            C35ContingencyPercent = 0m,
            C61ProjectContingencyPercent = 0m,
            C78DiscountRate = 10m
        };

        var result = Sut.ComputeLandBuilding(analysis, manyRows, input);
        var s = result.Summary;

        Assert.Equal(24, s.C18EstimatedDurationMonths);

        // Reading 2: (C78/100)^(C18/12) = (0.10)^(24/12) = (0.10)^2 = 0.01; C79 = 1/(1+0.01) ≈ 0.990
        double expectedC79 = 1.0 / (1.0 + Math.Pow(0.10, 24.0 / 12.0));
        Assert.Equal(Math.Round((decimal)expectedC79, 6), Math.Round(s.C79DiscountRateFactor!.Value, 6));

        // Reading 1 would give 1/(1.1^2) = 0.826 — assert we are NOT using Reading 1
        double reading1 = 1.0 / Math.Pow(1.10, 24.0 / 12.0);
        Assert.NotEqual(Math.Round((decimal)reading1, 4), Math.Round(s.C79DiscountRateFactor!.Value, 4));
    }

    // ── L&B: Duration calculation (C18) ──────────────────────────────────────

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
            C01TotalArea = 500m,
            C16EstSalesPeriod = 1,
            C35ContingencyPercent = 0m,
            C61ProjectContingencyPercent = 0m,
            C78DiscountRate = 0m
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input);

        Assert.Equal(700_000m, result.Summary.C38TotalProjectDevCost);
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

        Assert.Equal(400m, result.Summary.C28PublicUtilityArea);
        Assert.Equal(200_000m, result.Summary.C29PublicUtilityCost);
    }

    // ── L&B: CostItemKind lookup is description-independent ──────────────────

    [Fact]
    public void LandBuilding_AllocationPermitFee_LookedUpByKind_NotDescription()
    {
        // C-3 fix: renaming the description must not drop the fee from calc.
        var analysis = CreateLandBuildingAnalysis();
        var rows = new[] { MakeLbRow("M1", 100m, 1_000_000m) };

        // Add an AllocationPermitFee item with a custom description (user edited it)
        var permitItem = analysis.AddCostItem(HypothesisCostCategory.ProjectCost,
            CostItemKind.AllocationPermitFee, "Renamed Permit Fee", 1);
        permitItem.SetAmounts(50_000m);

        var input = new LandBuildingSummary
        {
            C01TotalArea = 100m,
            C16EstSalesPeriod = 1,
            C35ContingencyPercent = 0m,
            C61ProjectContingencyPercent = 0m,
            C78DiscountRate = 0m
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input);

        // C44 should equal 50,000 despite the renamed description
        Assert.Equal(50_000m, result.Summary.C44AllocationPermitFee);
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
        Assert.Equal(expectedE54, result.E57FinalRemainingValue);
    }

    [Fact]
    public void Condominium_ZeroDiscountRate_E58AndE59AreRounded()
    {
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

        Assert.Equal(5_130_000m, result.E58TotalAssetValueRounded);
        Assert.Equal(5_100m, result.E59TotalAssetValuePerSqM);
    }

    // ── Condo: Non-zero discount rate (Reading 2) ─────────────────────────────

    [Fact]
    public void Condominium_NonZeroDiscountRate_E54AppliesPercentageFactor_E56IsReading2()
    {
        // E55 = 12 (12%), E14 = 12 months
        // E54 = (E13 - E53) * (12/100) = residual * 0.12
        // E56 (Reading 2) = 1 / (1 + (0.12)^(12/12)) = 1 / (1.12) ≈ 0.8929
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
        decimal expectedE54 = residual * (12m / 100m);
        Assert.Equal(expectedE54, result.E54TotalRemainingValue);

        // Reading 2: (0.12)^(12/12) = 0.12; E56 = 1/(1+0.12) ≈ 0.8929
        double expectedE56 = 1.0 / (1.0 + Math.Pow(0.12, 1.0));
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

        Assert.Equal(120m, result.E09IndoorSalesArea);
        Assert.Equal(120m, result.E10ProjectSalesArea);
    }

    // ── Condo: E21 fallback mirrors E18 (C-5 fix) ────────────────────────────

    [Fact]
    public void Condominium_E21FallbackMirrorsE18_WhenNoRows()
    {
        // With no rows uploaded, d03=0. E18 and E21 should both fall back to E18SetAvgRoomSizeUnits.
        var analysis = CreateCondoAnalysis();
        var rows = new List<CondominiumUnitRow>();

        var input = new CondominiumSummary
        {
            E01AreaTitleDeed = 100m,
            E03FAR = 5m,
            E05TotalBuildingArea = 1000m,
            E14EstSalesDurationMonths = 6,
            E15CondoBuildingCostPerSqM = 0m,
            E18SetAvgRoomSizeUnits = 50,    // manual fallback
            E20FurniturePerUnit = 10_000m,
            E25HardCostContingencyPercent = 0m,
            E28EstConstructionPeriodMonths = 0,
            E46TransferFeePercent = 0m,
            E55DiscountRate = 0m
        };

        var result = Sut.ComputeCondominium(analysis, rows, input);

        // E18 and E21 should both be 50 (the manual fallback)
        Assert.Equal(50, result.E18SetAvgRoomSizeUnits);
        Assert.Equal(50, result.E21FurnitureQuantity);
        // E22 = E20 * E21 = 10,000 * 50 = 500,000
        Assert.Equal(500_000m, result.E22FurnitureTotal);
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
}
