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
///   - FSD C77: if C78=0 → C15-C76; else (C15-C76)*(C78/100)   [percentage applied]
///   - FSD C79: Reading 2 — 1 / (1 + (C78/100)^(C18/12))
///   - FSD C81: Round(C80, 10000)
///   - FSD C82: Round(C81/C01, 100)
///   - FSD E54: mirrors C77 conditional with E55/100
///   - FSD E56: Reading 2 — 1 / (1 + (E55/100)^(E14/12))
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
        => LandBuildingUnitRow.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "P1", "H1", model, landArea, price);

    private static CondominiumUnitRow MakeCondoRow(decimal usableArea, decimal price)
        => CondominiumUnitRow.Create(Guid.NewGuid(), Guid.NewGuid(), 1, 1, "A", "1A", "Studio", usableArea, price);

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
    public void LandBuilding_NonZeroDiscountRate_C77AppliesPercentageFactor_C79IsReading2()
    {
        // FSD C78 = 10 (10%), C18 = 1 month (from 1 unit / 1 per period)
        // C77 = (C15 - C76) * (10/100) = residual * 0.10
        // C79 (Reading 2) = 1 / (1 + (10/100)^(1/12)) = 1 / (1 + 0.10^(1/12)) ≈ 0.548
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

        // FSD C77 = (C15 - C76) * (C78/100)
        decimal residual = s.TotalRevenue!.Value - s.TotalDevCostsAndExpenses!.Value;
        decimal expectedC77 = residual * (10m / 100m);
        Assert.Equal(expectedC77, s.CurrentPropertyValue); // FSD C77

        // FSD C79 Reading 2: 1 / (1 + (C78/100)^(C18/12)) = 1 / (1 + (0.10)^(1/12))
        double expectedC79 = 1.0 / (1.0 + Math.Pow(0.10, 1.0 / 12.0));
        Assert.Equal(Math.Round((decimal)expectedC79, 6), Math.Round(s.DiscountRateFactor!.Value, 6));
        Assert.True(s.DiscountRateFactor < 1m);
    }

    [Fact]
    public void LandBuilding_NonZeroDiscountRate_C79Reading2_DivergenceFrom_Reading1()
    {
        // At FSD C78=10, C18=24 the two readings differ:
        // Reading 1: 1/(1.10)^2 = 0.826
        // Reading 2: 1/(1 + 0.10^2) = 1/(1.01) = 0.990
        var analysis = CreateLandBuildingAnalysis();
        var rows = new[] { MakeLbRow("M1", 50m, 1_000_000m) };

        // To get FSD C18=24 we need 24 units / 1 per period
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

        // Reading 2: (C78/100)^(C18/12) = (0.10)^(24/12) = (0.10)^2 = 0.01; C79 = 1/(1+0.01) ≈ 0.990
        double expectedC79 = 1.0 / (1.0 + Math.Pow(0.10, 24.0 / 12.0));
        Assert.Equal(Math.Round((decimal)expectedC79, 6), Math.Round(s.DiscountRateFactor!.Value, 6));

        // Reading 1 would give 1/(1.1^2) = 0.826 — assert we are NOT using Reading 1
        double reading1 = 1.0 / Math.Pow(1.10, 24.0 / 12.0);
        Assert.NotEqual(Math.Round((decimal)reading1, 4), Math.Round(s.DiscountRateFactor!.Value, 4));
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

    // ── L&B: Public utility cost ──────────────────────────────────────────────

    [Fact]
    public void LandBuilding_PublicUtilityCost_C29EqualC27TimesC01()
    {
        var analysis = CreateLandBuildingAnalysis();
        var rows = new[] { MakeLbRow("M1", 100m, 500_000m) };

        var input = new LandBuildingSummary
        {
            TotalArea = 400m,                        // FSD C01
            EstSalesPeriod = 1,                      // FSD C16
            PublicUtilityRatePerSqWa = 500m,          // FSD C27
            ContingencyPercent = 0m,                 // FSD C35
            ProjectContingencyPercent = 0m,          // FSD C61
            DiscountRate = 0m                        // FSD C78
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input);

        Assert.Equal(400m, result.Summary.PublicUtilityAreaForCost); // FSD C28
        Assert.Equal(200_000m, result.Summary.PublicUtilityCost);    // FSD C29
    }

    // ── L&B: CostItemKind lookup is description-independent ──────────────────

    [Fact]
    public void LandBuilding_AllocationPermitFee_LookedUpByKind_NotDescription()
    {
        // FSD C44 fix: renaming the description must not drop the fee from calc.
        var analysis = CreateLandBuildingAnalysis();
        var rows = new[] { MakeLbRow("M1", 100m, 1_000_000m) };

        // Add an AllocationPermitFee item with a custom description (user edited it)
        var permitItem = analysis.AddCostItem(HypothesisCostCategory.ProjectCost,
            CostItemKind.AllocationPermitFee, "Renamed Permit Fee", 1);
        permitItem.SetAmounts(50_000m);

        var input = new LandBuildingSummary
        {
            TotalArea = 100m,                // FSD C01
            EstSalesPeriod = 1,              // FSD C16
            ContingencyPercent = 0m,         // FSD C35
            ProjectContingencyPercent = 0m,  // FSD C61
            DiscountRate = 0m                // FSD C78
        };

        var result = Sut.ComputeLandBuilding(analysis, rows, input);

        // FSD C44 should equal 50,000 despite the renamed description
        Assert.Equal(50_000m, result.Summary.AllocationPermitFee); // FSD C44
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

    // ── Condo: Non-zero discount rate (Reading 2) ─────────────────────────────

    [Fact]
    public void Condominium_NonZeroDiscountRate_E54AppliesPercentageFactor_E56IsReading2()
    {
        // FSD E55 = 12 (12%), E14 = 12 months
        // E54 = (E13 - E53) * (12/100) = residual * 0.12
        // E56 (Reading 2) = 1 / (1 + (0.12)^(12/12)) = 1 / (1.12) ≈ 0.8929
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

        decimal residual = result.TotalRevenue!.Value - result.TotalDevCosts!.Value;
        decimal expectedE54 = residual * (12m / 100m);
        Assert.Equal(expectedE54, result.TotalRemainingValue); // FSD E54

        // Reading 2: (0.12)^(12/12) = 0.12; E56 = 1/(1+0.12) ≈ 0.8929
        double expectedE56 = 1.0 / (1.0 + Math.Pow(0.12, 1.0));
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
}
