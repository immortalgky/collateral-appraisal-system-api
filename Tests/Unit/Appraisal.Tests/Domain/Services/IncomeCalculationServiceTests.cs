using System.Text.Json;
using Appraisal.Domain.Appraisals.Income;
using Appraisal.Domain.Appraisals.Income.MethodDetails;
using Appraisal.Domain.Services;

namespace Appraisal.Tests.Domain.Services;

/// <summary>
/// Unit tests for <see cref="IncomeCalculationService"/>.
/// Each test verifies per-year output against hand-calculated values
/// derived directly from the TypeScript derived-rules reference.
/// Tolerance: 0.01m for floating-point steps; exact equality for integer arithmetic.
/// </summary>
public class IncomeCalculationServiceTests
{
    private readonly IncomeCalculationService _sut = new();

    // ── Helper helpers ────────────────────────────────────────────────────

    private static IncomeAnalysis BuildMinimalAnalysis(
        int years,
        decimal capRate,
        decimal discountedRate,
        int daysInYear = 365,
        string sectionType = "income")
    {
        var analysis = IncomeAnalysis.Create(
            pricingAnalysisMethodId: Guid.NewGuid(),
            templateCode: "test",
            templateName: "Test",
            totalNumberOfYears: years,
            totalNumberOfDayInYear: daysInYear,
            capitalizeRate: capRate,
            discountedRate: discountedRate);

        return analysis;
    }

    private static string Serialize(object detail)
        => JsonSerializer.Serialize(detail, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    private static IncomeAnalysis BuildAnalysisWithSingleAssumption(
        int years,
        decimal capRate,
        decimal discountedRate,
        string methodCode,
        string detailJson,
        string sectionType = "income",
        string sectionIdentifier = "positive")
    {
        var analysis = BuildMinimalAnalysis(years, capRate, discountedRate);

        var section = IncomeSection.Create(analysis.Id, sectionType, "Test Section", sectionIdentifier, 1);
        var category = section.AddCategory("income", "Test Category", "positive", 1);
        category.AddAssumption("I00", "Test Assumption", "positive", 1, methodCode, detailJson);

        // Add a summaryDCF section so the DCF pipeline runs.
        var summarySection = IncomeSection.Create(analysis.Id, "summaryDCF", "Summary", "empty", 2);
        analysis.ReplaceSections([section, summarySection]);

        return analysis;
    }

    // ── Step-compounding helper tests ─────────────────────────────────────

    [Fact]
    public void StepCompounding_Year0_ReturnsFirstYearAmt()
    {
        var result = IncomeCalculationService.ComputeStepCompoundingArray(1000m, 5m, 1, 1);
        Assert.Equal(1000m, result[0]);
    }

    [Fact]
    public void StepCompounding_AppliesGrowthOnMultipleOfYrs()
    {
        // firstYearAmt=1000, rate=10%, every 2 years, 5 years
        // y0=1000, y1=1000, y2=1100, y3=1100, y4=1210
        var result = IncomeCalculationService.ComputeStepCompoundingArray(1000m, 10m, 2, 5);
        Assert.Equal(1000m, result[0]);
        Assert.Equal(1000m, result[1]);
        Assert.Equal(1100m, result[2]);
        Assert.Equal(1100m, result[3]);
        Assert.Equal(1210m, result[4]);
    }

    [Fact]
    public void StepCompounding_ZeroIncreaseRateYrs_NoGrowth()
    {
        var result = IncomeCalculationService.ComputeStepCompoundingArray(500m, 10m, 0, 3);
        Assert.Equal(500m, result[0]);
        Assert.Equal(500m, result[1]);
        Assert.Equal(500m, result[2]);
    }

    // ── Occupancy rate helper tests ───────────────────────────────────────

    [Fact]
    public void OccupancyRate_Year0_ReturnsFirstYearPct()
    {
        var result = IncomeCalculationService.ComputeOccupancyRate(60m, 5m, 2, 1);
        Assert.Equal(60m, result[0]);
    }

    [Fact]
    public void OccupancyRate_StepsUpOnMultiple()
    {
        // first=60, +5 every 2 years → y0=60, y1=60, y2=65, y3=65, y4=70
        var result = IncomeCalculationService.ComputeOccupancyRate(60m, 5m, 2, 5);
        Assert.Equal(60m, result[0]);
        Assert.Equal(60m, result[1]);
        Assert.Equal(65m, result[2]);
        Assert.Equal(65m, result[3]);
        Assert.Equal(70m, result[4]);
    }

    [Fact]
    public void OccupancyRate_ClampsAt100()
    {
        // TS code: if (prevOccupancyRate >= 100) return 100; — check happens BEFORE the addition.
        // y0=98, y1: prev=98 < 100, 1%1=0 → add: 98+5=103 (no clamp on the value itself)
        // y2: prev=103 >= 100 → return 100
        // y3: prev=100 >= 100 → return 100
        var result = IncomeCalculationService.ComputeOccupancyRate(98m, 5m, 1, 4);
        Assert.Equal(98m, result[0]);
        Assert.Equal(103m, result[1]); // addition not clamped
        Assert.Equal(100m, result[2]); // prev>=100 triggers clamp
        Assert.Equal(100m, result[3]);
    }

    // ── Method 01 ─────────────────────────────────────────────────────────

    [Fact]
    public void Method01_ComputesRoomIncomeCorrectly()
    {
        // 3 years, daysInYear=365
        // sumSaleableArea=10, avgRoomRate=1000, increaseRatePct=5, increaseRateYrs=1
        // occupancyRateFirstYearPct=60, occupancyRatePct=5, occupancyRateYrs=1
        // saleableArea[y] = 10 × 365 = 3650 (constant)
        // occupancyRate step (TS: if y % occupancyRateYrs == 0 add pct):
        //   y0 = 60 (first year, no growth)
        //   y1 = 60 + 5 = 65 (y=1, 1%1=0)
        //   y2 = 65 + 5 = 70 (y=2, 2%1=0)
        // avgDailyRate: y0=1000, y1=1000×1.05=1050, y2=1050×1.05=1102.5
        // roomIncome[y] = (saleableArea × (occ/100)) × avgDailyRate
        // y0 = 3650 × 0.60 × 1000 = 2,190,000
        // y1 = 3650 × 0.65 × 1050 = 2,491,125
        // y2 = 3650 × 0.70 × 1102.5 = 2,816,887.50

        var detail = new Method01Detail
        {
            SumSaleableArea = 10m,
            AvgRoomRate = 1000m,
            IncreaseRatePct = 5m,
            IncreaseRateYrs = 1,
            OccupancyRateFirstYearPct = 60m,
            OccupancyRatePct = 5m,
            OccupancyRateYrs = 1
        };

        var analysis = BuildAnalysisWithSingleAssumption(3, 5m, 8m, "01", Serialize(detail));
        var result = _sut.Calculate(analysis);

        var values = result.MethodValues.Values.First();
        Assert.Equal(3, values.Length);
        Assert.Equal(2_190_000m, values[0]);
        Assert.Equal(2_491_125m, values[1]);    // 3650 × 0.65 × 1050
        Assert.Equal(2_816_887.5m, values[2]); // 3650 × 0.70 × 1102.5
    }

    // ── Method 02 ─────────────────────────────────────────────────────────

    [Fact]
    public void Method02_ComputesSeasonalRoomIncomeCorrectly()
    {
        // Method 02 uses totalSaleableArea (not sumSaleableArea).
        // totalSaleableArea=10, avgRoomRate=1200, increaseRatePct=0, increaseRateYrs=1
        // occupancyRateFirstYearPct=70, occupancyRatePct=0, occupancyRateYrs=1
        // daysInYear=365
        // saleableArea = 10 × 365 = 3650
        // occ: y0=70, y1=70, y2=70
        // avgDailyRate: all years = 1200 (no growth)
        // y0=y1=y2 = 3650 × 0.70 × 1200 = 3,066,000

        var detail = new Method02Detail
        {
            TotalSaleableArea = 10m,
            AvgRoomRate = 1200m,
            IncreaseRatePct = 0m,
            IncreaseRateYrs = 1,
            OccupancyRateFirstYearPct = 70m,
            OccupancyRatePct = 0m,
            OccupancyRateYrs = 1
        };

        var analysis = BuildAnalysisWithSingleAssumption(3, 5m, 8m, "02", Serialize(detail));
        var result = _sut.Calculate(analysis);
        var values = result.MethodValues.Values.First();

        Assert.Equal(3, values.Length);
        Assert.Equal(3_066_000m, values[0]);
        Assert.Equal(3_066_000m, values[1]);
        Assert.Equal(3_066_000m, values[2]);
    }

    // ── Method 03 ─────────────────────────────────────────────────────────

    [Fact]
    public void Method03_StepCompoundsFromFirstYearAmt()
    {
        // firstYearAmt=500000, increaseRatePct=3, increaseRateYrs=2, years=4
        // y0=500000, y1=500000, y2=515000, y3=515000
        var detail = new Method03Detail
        {
            FirstYearAmt = 500_000m,
            IncreaseRatePct = 3m,
            IncreaseRateYrs = 2
        };

        var analysis = BuildAnalysisWithSingleAssumption(4, 5m, 8m, "03", Serialize(detail));
        var result = _sut.Calculate(analysis);
        var values = result.MethodValues.Values.First();

        Assert.Equal(4, values.Length);
        Assert.Equal(500_000m, values[0]);
        Assert.Equal(500_000m, values[1]);
        Assert.Equal(515_000m, values[2]);
        Assert.Equal(515_000m, values[3]);
    }

    // ── Method 04 ─────────────────────────────────────────────────────────

    [Fact]
    public void Method04_StepCompoundsWithOccupancyRate()
    {
        // firstYearAmt=1000000, increaseRatePct=5, increaseRateYrs=1
        // occupancyRateFirstYearPct=80, occupancyRatePct=0, occupancyRateYrs=1 (stable occ)
        // adjusted[y]: y0=1000000, y1=1050000, y2=1102500
        // roomIncome[y] = adjusted × (occ/100):
        // y0=1000000×0.8=800000, y1=1050000×0.8=840000, y2=1102500×0.8=882000
        var detail = new Method04Detail
        {
            FirstYearAmt = 1_000_000m,
            IncreaseRatePct = 5m,
            IncreaseRateYrs = 1,
            OccupancyRateFirstYearPct = 80m,
            OccupancyRatePct = 0m,
            OccupancyRateYrs = 1
        };

        var analysis = BuildAnalysisWithSingleAssumption(3, 5m, 8m, "04", Serialize(detail));
        var result = _sut.Calculate(analysis);
        var values = result.MethodValues.Values.First();

        Assert.Equal(800_000m, values[0]);
        Assert.Equal(840_000m, values[1]);
        Assert.Equal(882_000m, values[2]);
    }

    // ── Method 05 ─────────────────────────────────────────────────────────

    [Fact]
    public void Method05_StepCompoundsFromAnnualTotal()
    {
        // sumRoomIncomePerYear=120000, increaseRatePct=5, increaseRateYrs=1
        // y0=120000, y1=126000, y2=132300
        var detail = new Method05Detail
        {
            SumRoomIncomePerYear = 120_000m,
            IncreaseRatePct = 5m,
            IncreaseRateYrs = 1
        };

        var analysis = BuildAnalysisWithSingleAssumption(3, 5m, 8m, "05", Serialize(detail));
        var result = _sut.Calculate(analysis);
        var values = result.MethodValues.Values.First();

        Assert.Equal(120_000m, values[0]);
        Assert.Equal(126_000m, values[1]);
        Assert.Equal(132_300m, values[2]);
    }

    // ── Method 06 ─────────────────────────────────────────────────────────

    [Fact]
    public void Method06_ComputesRentalIncomePerSqm()
    {
        // sumSaleableArea=1000, avgRentalRatePerMonth=150, increaseRatePct=0, increaseRateYrs=1
        // occupancyRateFirstYearPct=90, occupancyRatePct=0, occupancyRateYrs=1
        // avgRentalRate[y]=150 (no growth)
        // saleableAreaDeduct = 1000 × 0.90 = 900
        // totalRentalIncome[y] = 150 × 900 × 12 = 1,620,000
        var detail = new Method06Detail
        {
            SumSaleableArea = 1000m,
            AvgRentalRatePerMonth = 150m,
            IncreaseRatePct = 0m,
            IncreaseRateYrs = 1,
            OccupancyRateFirstYearPct = 90m,
            OccupancyRatePct = 0m,
            OccupancyRateYrs = 1
        };

        var analysis = BuildAnalysisWithSingleAssumption(3, 5m, 8m, "06", Serialize(detail));
        var result = _sut.Calculate(analysis);
        var values = result.MethodValues.Values.First();

        Assert.Equal(1_620_000m, values[0]);
        Assert.Equal(1_620_000m, values[1]);
        Assert.Equal(1_620_000m, values[2]);
    }

    // ── Method 07 ─────────────────────────────────────────────────────────

    [Fact]
    public void Method07_StepCompoundsFromAnnualExpenseTotal()
    {
        // sumTotalRoomExpensePerYear=200000, increaseRatePct=3, increaseRateYrs=1
        // y0=200000, y1=206000, y2=212180
        var detail = new Method07Detail
        {
            SumTotalRoomExpensePerYear = 200_000m,
            IncreaseRatePct = 3m,
            IncreaseRateYrs = 1
        };

        var analysis = BuildAnalysisWithSingleAssumption(3, 5m, 8m, "07", Serialize(detail));
        var result = _sut.Calculate(analysis);
        var values = result.MethodValues.Values.First();

        Assert.Equal(200_000m, values[0]);
        Assert.Equal(206_000m, values[1]);
        Assert.Equal(212_180m, values[2]);
    }

    // ── Method 08 ─────────────────────────────────────────────────────────

    [Fact]
    public void Method08_MultipliesByMethod01SaleableAreaOccRate()
    {
        // Method 01 first: sumSaleableArea=10, daysInYear=365, occ=60%, no growth, avgRoomRate=1000
        // saleableAreaDeduct[y] = 10×365×0.60 = 2190 (constant)
        //
        // Method 08: firstYearAmt=50, increaseRatePct=0, increaseRateYrs=1
        // totalFoodAndBeveragePerRoomPerDay[y] = 50 (no growth)
        // totalMethodValues[y] = 50 × 2190 = 109500

        var analysis = IncomeAnalysis.Create(Guid.NewGuid(), "test", "Test", 3, 365, 5m, 8m);
        var section = IncomeSection.Create(analysis.Id, "income", "Income", "positive", 1);
        var category = section.AddCategory("income", "Cat", "positive", 1);

        var detail01 = new Method01Detail
        {
            SumSaleableArea = 10m,
            AvgRoomRate = 1000m,
            IncreaseRatePct = 0m,
            IncreaseRateYrs = 1,
            OccupancyRateFirstYearPct = 60m,
            OccupancyRatePct = 0m,
            OccupancyRateYrs = 1
        };
        category.AddAssumption("I01", "Room Income", "positive", 1, "01", Serialize(detail01));

        var detail08 = new Method08Detail
        {
            FirstYearAmt = 50m,
            IncreaseRatePct = 0m,
            IncreaseRateYrs = 1
        };
        category.AddAssumption("E08", "F&B", "positive", 2, "08", Serialize(detail08));

        var summarySection = IncomeSection.Create(analysis.Id, "summaryDCF", "Summary", "empty", 2);
        analysis.ReplaceSections([section, summarySection]);

        var result = _sut.Calculate(analysis);
        var method08Values = result.MethodValues
            .Single(kvp => kvp.Value.All(v => v == 109_500m)).Value;

        Assert.Equal(109_500m, method08Values[0]);
        Assert.Equal(109_500m, method08Values[1]);
        Assert.Equal(109_500m, method08Values[2]);
    }

    // ── Method 09 ─────────────────────────────────────────────────────────

    [Fact]
    public void Method09_StepCompoundsFromAnnualSalaryTotal()
    {
        // sumTotalSalaryPerYear=3_000_000, increaseRatePct=5, increaseRateYrs=2
        // y0=3000000, y1=3000000, y2=3150000, y3=3150000
        var detail = new Method09Detail
        {
            SumTotalSalaryPerYear = 3_000_000m,
            IncreaseRatePct = 5m,
            IncreaseRateYrs = 2
        };

        var analysis = BuildAnalysisWithSingleAssumption(4, 5m, 8m, "09", Serialize(detail));
        var result = _sut.Calculate(analysis);
        var values = result.MethodValues.Values.First();

        Assert.Equal(3_000_000m, values[0]);
        Assert.Equal(3_000_000m, values[1]);
        Assert.Equal(3_150_000m, values[2]);
        Assert.Equal(3_150_000m, values[3]);
    }

    // ── Method 10 ─────────────────────────────────────────────────────────

    [Fact]
    public void Method10_PassesThroughTotalPropertyTaxArray()
    {
        var detail = new Method10Detail
        {
            PropertyTax = new Method10Detail.PropertyTaxDetail
            {
                TotalPropertyTax = [100_000m, 100_000m, 100_000m]
            }
        };

        var analysis = BuildAnalysisWithSingleAssumption(3, 5m, 8m, "10", Serialize(detail));
        var result = _sut.Calculate(analysis);
        var values = result.MethodValues.Values.First();

        Assert.Equal(100_000m, values[0]);
        Assert.Equal(100_000m, values[1]);
        Assert.Equal(100_000m, values[2]);
    }

    [Fact]
    public void Method10_RepeatsLastValueWhenArrayShorterThanYears()
    {
        var detail = new Method10Detail
        {
            PropertyTax = new Method10Detail.PropertyTaxDetail
            {
                TotalPropertyTax = [100_000m]
            }
        };

        var analysis = BuildAnalysisWithSingleAssumption(3, 5m, 8m, "10", Serialize(detail));
        var result = _sut.Calculate(analysis);
        var values = result.MethodValues.Values.First();

        Assert.Equal(100_000m, values[0]);
        Assert.Equal(100_000m, values[1]);
        Assert.Equal(100_000m, values[2]);
    }

    // ── Method 11 ─────────────────────────────────────────────────────────

    [Fact]
    public void Method11_MultipliesByMethod06SaleableAreaOccRateAnd12()
    {
        // Method 06: sumSaleableArea=1000, avgRentalRatePerMonth=150, occ=90%, no growth
        // saleableAreaDeductByOccRate[y] = 1000 × 0.90 = 900
        //
        // Method 11: energyCostIndex=200, increaseRatePct=0, increaseRateYrs=1
        // energyCostIndexIncrease[y] = 200
        // totalMethodValues[y] = 200 × 900 × 12 = 2,160,000

        var analysis = IncomeAnalysis.Create(Guid.NewGuid(), "test", "Test", 3, 365, 5m, 8m);
        var section = IncomeSection.Create(analysis.Id, "income", "Income", "positive", 1);
        var category = section.AddCategory("income", "Cat", "positive", 1);

        var detail06 = new Method06Detail
        {
            SumSaleableArea = 1000m,
            AvgRentalRatePerMonth = 150m,
            IncreaseRatePct = 0m,
            IncreaseRateYrs = 1,
            OccupancyRateFirstYearPct = 90m,
            OccupancyRatePct = 0m,
            OccupancyRateYrs = 1
        };
        category.AddAssumption("I06", "Rental", "positive", 1, "06", Serialize(detail06));

        var detail11 = new Method11Detail
        {
            EnergyCostIndex = 200m,
            IncreaseRatePct = 0m,
            IncreaseRateYrs = 1
        };
        category.AddAssumption("E11", "Energy", "positive", 2, "11", Serialize(detail11));

        var summarySection = IncomeSection.Create(analysis.Id, "summaryDCF", "Summary", "empty", 2);
        analysis.ReplaceSections([section, summarySection]);

        var result = _sut.Calculate(analysis);
        var method11Values = result.MethodValues
            .Single(kvp => kvp.Value[0] == 2_160_000m).Value;

        Assert.Equal(2_160_000m, method11Values[0]);
        Assert.Equal(2_160_000m, method11Values[1]);
        Assert.Equal(2_160_000m, method11Values[2]);
    }

    // ── Method 12 ─────────────────────────────────────────────────────────

    [Fact]
    public void Method12_ProportionOfNewReplacementCost()
    {
        // newReplacementCost=10_000_000, proportionPct=2, increaseRatePct=3, increaseRateYrs=1
        // y0 = (2/100) × 10_000_000 = 200_000
        // y1 = 200_000 × 1.03 = 206_000
        // y2 = 206_000 × 1.03 = 212_180
        var detail = new Method12Detail
        {
            NewReplacementCost = 10_000_000m,
            ProportionPct = 2m,
            IncreaseRatePct = 3m,
            IncreaseRateYrs = 1
        };

        var analysis = BuildAnalysisWithSingleAssumption(3, 5m, 8m, "12", Serialize(detail));
        var result = _sut.Calculate(analysis);
        var values = result.MethodValues.Values.First();

        Assert.Equal(200_000m, values[0]);
        Assert.Equal(206_000m, values[1]);
        Assert.Equal(212_180m, values[2]);
    }

    // ── Method 13 ─────────────────────────────────────────────────────────

    [Fact]
    public void Method13_ResolvesAssumptionReferenceByDbId()
    {
        // Assumption A: method 03, firstYearAmt=1000000, no growth, years=3 → [1000000, 1000000, 1000000]
        // Assumption B: method 13, proportionPct=10, refTarget.kind="assumption", refTarget.dbId=A.Id
        // Expected B: [100000, 100000, 100000]

        var analysis = IncomeAnalysis.Create(Guid.NewGuid(), "test", "Test", 3, 365, 5m, 8m);
        var section = IncomeSection.Create(analysis.Id, "income", "Income", "positive", 1);
        var category = section.AddCategory("income", "Cat", "positive", 1);

        var detailA = new Method03Detail { FirstYearAmt = 1_000_000m, IncreaseRatePct = 0m, IncreaseRateYrs = 1 };
        var assumptionA = category.AddAssumption("I03", "Base Income", "positive", 1, "03", Serialize(detailA));

        var detailB = new Method13Detail
        {
            ProportionPct = 10m,
            RefTarget = new RefTarget { Kind = "assumption", DbId = assumptionA.Id.ToString() }
        };
        category.AddAssumption("M99", "Proportional", "positive", 2, "13", Serialize(detailB));

        var summarySection = IncomeSection.Create(analysis.Id, "summaryDCF", "Summary", "empty", 2);
        analysis.ReplaceSections([section, summarySection]);

        var result = _sut.Calculate(analysis);

        var aValues = result.MethodValues[assumptionA.Id];
        Assert.Equal(1_000_000m, aValues[0]);

        var bId = category.Assumptions.Last().Id;
        var bValues = result.MethodValues[bId];
        Assert.Equal(100_000m, bValues[0]);
        Assert.Equal(100_000m, bValues[1]);
        Assert.Equal(100_000m, bValues[2]);
    }

    // ── Method 14 ─────────────────────────────────────────────────────────

    [Fact]
    public void Method14_StepCompoundsFromFirstYearAmt()
    {
        // firstYearAmt=250000, increaseRatePct=4, increaseRateYrs=1
        // y0=250000, y1=260000, y2=270400
        var detail = new Method14Detail
        {
            FirstYearAmt = 250_000m,
            IncreaseRatePct = 4m,
            IncreaseRateYrs = 1
        };

        var analysis = BuildAnalysisWithSingleAssumption(3, 5m, 8m, "14", Serialize(detail));
        var result = _sut.Calculate(analysis);
        var values = result.MethodValues.Values.First();

        Assert.Equal(250_000m, values[0]);
        Assert.Equal(260_000m, values[1]);
        Assert.Equal(270_400m, values[2]);
    }

    // ── DCF Summary pipeline ──────────────────────────────────────────────

    [Fact]
    public void DcfSummary_TerminalRevenueAtSecondToLastIndex()
    {
        // grossRevenue[N-1] / (capRate/100) placed at index N-2 in terminalRevenue array.
        // 3 years, capRate=5%, grossRevenue[2]=1_000_000
        // terminalRevenue: length=2, index[1]=1000000/(5/100)=20_000_000
        var detail = new Method03Detail { FirstYearAmt = 1_000_000m, IncreaseRatePct = 0m, IncreaseRateYrs = 1 };
        var analysis = BuildAnalysisWithSingleAssumption(3, 5m, 8m, "03", Serialize(detail));
        var result = _sut.Calculate(analysis);

        Assert.Equal(2, result.TerminalRevenue.Length); // N-1 = 2
        Assert.Equal(0m, result.TerminalRevenue[0]);
        Assert.Equal(20_000_000m, result.TerminalRevenue[1]); // index N-2 = 1
    }

    [Fact]
    public void DcfSummary_FinalValueEqualsSumOfPresentValues()
    {
        var detail = new Method03Detail { FirstYearAmt = 2_000_000m, IncreaseRatePct = 3m, IncreaseRateYrs = 1 };
        var analysis = BuildAnalysisWithSingleAssumption(5, 5m, 8m, "03", Serialize(detail));
        var result = _sut.Calculate(analysis);

        var sumPv = result.PresentValue.Sum();
        Assert.Equal(Math.Round(result.FinalValue, 6), Math.Round(sumPv, 6));
    }

    [Fact]
    public void DcfSummary_DiscountFactorFormula()
    {
        // discount[i] = 1 / (1 + discountedRate/100)^(i+1)
        // discountedRate=8, i=0 → 1/1.08 ≈ 0.925926
        var detail = new Method03Detail { FirstYearAmt = 1_000_000m, IncreaseRatePct = 0m, IncreaseRateYrs = 1 };
        var analysis = BuildAnalysisWithSingleAssumption(3, 5m, 8m, "03", Serialize(detail));
        var result = _sut.Calculate(analysis);

        var expectedDiscount0 = 1m / (decimal)Math.Pow(1.08, 1);
        Assert.Equal(Math.Round(expectedDiscount0, 8), Math.Round(result.Discount[0], 8));
    }

    [Fact]
    public void DcfSummary_DiscountedRateZeroGivesDiscountOf1()
    {
        var detail = new Method03Detail { FirstYearAmt = 1_000_000m, IncreaseRatePct = 0m, IncreaseRateYrs = 1 };
        var analysis = BuildAnalysisWithSingleAssumption(3, 5m, 0m, "03", Serialize(detail));
        var result = _sut.Calculate(analysis);

        Assert.All(result.Discount, d => Assert.Equal(1m, d));
    }

    [Fact]
    public void DcfSummary_CapRateZeroGivesZeroTerminalRevenue()
    {
        var detail = new Method03Detail { FirstYearAmt = 1_000_000m, IncreaseRatePct = 0m, IncreaseRateYrs = 1 };
        var analysis = BuildAnalysisWithSingleAssumption(3, 0m, 8m, "03", Serialize(detail));
        var result = _sut.Calculate(analysis);

        Assert.All(result.TerminalRevenue, tr => Assert.Equal(0m, tr));
    }

    // ── Direct capitalization (summaryDirect) ─────────────────────────────

    [Fact]
    public void DirectCap_FinalValueIsNOIDividedByCapRatePercent()
    {
        // NOI = 1_200_000 (year 0 income), capRate = 6 (meaning 6%).
        // Backend uses the correct financial formula: totalNet / (capRate / 100)
        //   = 1_200_000 / 0.06 = 20_000_000.
        //
        // Deviation from TS: The TS literal `totalNet / capitalizeRate / 100` evaluates to
        //   1_200_000 / 6 / 100 = 2_000 due to left-associative division. That is
        //   financially incorrect (divides by capRate*100 rather than capRate/100).
        //   The backend deliberately uses the correct formula and is called out as a
        //   deviation from the buggy TS formula for summaryDirect.

        var detail = new Method03Detail { FirstYearAmt = 1_200_000m, IncreaseRatePct = 0m, IncreaseRateYrs = 1 };

        var analysis = IncomeAnalysis.Create(Guid.NewGuid(), "direct-apartment", "Direct Apt", 1, 365, 6m, 0m);
        var incomeSection = IncomeSection.Create(analysis.Id, "income", "Income", "positive", 1);
        var category = incomeSection.AddCategory("income", "Cat", "positive", 1);
        category.AddAssumption("I00", "NOI", "positive", 1, "03", Serialize(detail));

        var summarySection = IncomeSection.Create(analysis.Id, "summaryDirect", "Summary", "empty", 2);
        analysis.ReplaceSections([incomeSection, summarySection]);

        var result = _sut.Calculate(analysis);

        // Correct formula: totalNet / (capRate / 100)
        var expectedFinalValue = 1_200_000m / (6m / 100m); // = 20_000_000
        Assert.Equal(expectedFinalValue, result.FinalValue);
    }

    // ── GrossRevenue with positive / negative sections ────────────────────

    [Fact]
    public void GrossRevenue_SubtractsNegativeSections()
    {
        // Income section (positive): 2_000_000 per year
        // Expenses section (negative): 500_000 per year
        // grossRevenue = 2_000_000 − 500_000 = 1_500_000 per year

        var analysis = IncomeAnalysis.Create(Guid.NewGuid(), "test", "Test", 3, 365, 5m, 8m);

        var incomeSection = IncomeSection.Create(analysis.Id, "income", "Income", "positive", 1);
        var incCat = incomeSection.AddCategory("income", "Income Cat", "positive", 1);
        incCat.AddAssumption("I00", "Income", "positive", 1, "03",
            Serialize(new Method03Detail { FirstYearAmt = 2_000_000m, IncreaseRatePct = 0m, IncreaseRateYrs = 1 }));

        var expSection = IncomeSection.Create(analysis.Id, "expenses", "Expenses", "negative", 2);
        var expCat = expSection.AddCategory("expenses", "Expense Cat", "positive", 1);
        expCat.AddAssumption("E00", "Expense", "positive", 1, "14",
            Serialize(new Method14Detail { FirstYearAmt = 500_000m, IncreaseRatePct = 0m, IncreaseRateYrs = 1 }));

        var summarySection = IncomeSection.Create(analysis.Id, "summaryDCF", "Summary", "empty", 3);
        analysis.ReplaceSections([incomeSection, expSection, summarySection]);

        var result = _sut.Calculate(analysis);

        Assert.Equal(1_500_000m, result.GrossRevenue[0]);
        Assert.Equal(1_500_000m, result.GrossRevenue[1]);
        Assert.Equal(1_500_000m, result.GrossRevenue[2]);
    }

    // ── ApplyCalculationResult mutates aggregate ──────────────────────────

    [Fact]
    public void ApplyCalculationResult_UpdatesJsonColumnsAndSummary()
    {
        var detail = new Method03Detail { FirstYearAmt = 1_000_000m, IncreaseRatePct = 5m, IncreaseRateYrs = 1 };
        var analysis = BuildAnalysisWithSingleAssumption(3, 5m, 8m, "03", Serialize(detail));

        var result = _sut.Calculate(analysis);
        analysis.ApplyCalculationResult(result);

        Assert.NotNull(analysis.FinalValue);
        Assert.True(analysis.FinalValue > 0m);

        // Summary JSON should be non-empty arrays.
        Assert.NotEqual("[]", analysis.Summary.GrossRevenueJson);
        Assert.NotEqual("[]", analysis.Summary.PresentValueJson);
        Assert.NotEqual("[]", analysis.Summary.DiscountJson);

        // Method and assumption JSON on the assumption itself.
        var assumption = analysis.Sections.First().Categories.First().Assumptions.First();
        Assert.NotEqual("[]", assumption.Method.TotalMethodValuesJson);
        Assert.NotEqual("[]", assumption.TotalAssumptionValuesJson);
    }

    // ── Direct-cap summary arrays (M-4) ──────────────────────────────────

    [Fact]
    public void DirectCap_SummaryArraysAreSingleElement()
    {
        // For summaryDirect templates (direct-apartment), all summary arrays must have length 1
        // so the FE can render every column without conditional branching.
        var detail = new Method03Detail { FirstYearAmt = 1_200_000m, IncreaseRatePct = 0m, IncreaseRateYrs = 1 };

        var analysis = IncomeAnalysis.Create(Guid.NewGuid(), "direct-apartment", "Direct Apt", 1, 365, 6m, 0m);
        var incomeSection = IncomeSection.Create(analysis.Id, "income", "Income", "positive", 1);
        var category = incomeSection.AddCategory("income", "Cat", "positive", 1);
        category.AddAssumption("I00", "NOI", "positive", 1, "03", Serialize(detail));

        var summarySection = IncomeSection.Create(analysis.Id, "summaryDirect", "Summary", "empty", 2);
        analysis.ReplaceSections([incomeSection, summarySection]);

        var result = _sut.Calculate(analysis);

        // All summary arrays must have exactly 1 element (length == totalNumberOfYears)
        Assert.Equal(1, result.TerminalRevenue.Length);
        Assert.Equal(1, result.TotalNet.Length);
        Assert.Equal(1, result.Discount.Length);
        Assert.Equal(1, result.PresentValue.Length);

        // Correct values for single-element direct-cap arrays
        Assert.Equal(0m, result.TerminalRevenue[0]);
        Assert.Equal(1_200_000m, result.TotalNet[0]);      // grossRevenue[0]
        Assert.Equal(1m, result.Discount[0]);
        Assert.Equal(20_000_000m, result.PresentValue[0]); // 1_200_000 / (6/100)
    }

    // ── FinalValueRounded preservation ───────────────────────────────────

    [Fact]
    public void Calculate_PreservesFinalValueRoundedWhenAlreadySet()
    {
        // FinalValueRounded is set to a custom value on the analysis — it should be preserved.
        var detail = new Method03Detail { FirstYearAmt = 1_000_000m, IncreaseRatePct = 5m, IncreaseRateYrs = 1 };
        var analysis = BuildAnalysisWithSingleAssumption(3, 5m, 8m, "03", Serialize(detail));

        // Simulate a previously saved custom rounded value.
        analysis.SetComputedValues(0m, 99_999_999m, IncomeSummary.Empty());

        var result = _sut.Calculate(analysis);
        Assert.Equal(99_999_999m, result.FinalValueRounded);
    }

    // ── Method 10 — server-side bracket derivation ────────────────────────

    /// <summary>
    /// Seeded brackets (from 20260414180000_SeedData_PricingParameters.sql):
    ///   Tier 1: rate=0.02, min=10_000_000, max=50_000_000
    ///   Tier 2: rate=0.03, min=50_000_001, max=75_000_000
    ///   Tier 3: rate=0.05, min=75_000_001, max=100_000_000
    ///   Tier 4: rate=0.10, min=100_000_001, max=null
    /// </summary>
    private static readonly IReadOnlyList<Parameter.Contracts.PricingParameters.TaxBracketDto> SeededBrackets =
    [
        new(1, 0.02m,   10_000_000m,  50_000_000m),
        new(2, 0.03m,   50_000_001m,  75_000_000m),
        new(3, 0.05m,   75_000_001m, 100_000_000m),
        new(4, 0.10m,  100_000_001m,          null),
    ];

    [Fact]
    public void Method10_WithBrackets_DerivesTaxFromTotalPropertyPrice()
    {
        // TotalPropertyPrice = 20_000_000 → Tier 1 (rate 0.02)
        // Expected tax = 20_000_000 × 0.02 = 400_000
        var detail = new Method10Detail
        {
            PropertyTax = new Method10Detail.PropertyTaxDetail
            {
                TotalPropertyPrice = [20_000_000m, 20_000_000m, 20_000_000m],
                TotalPropertyTax   = [999_999m,    999_999m,    999_999m]   // client-supplied, should be overwritten
            }
        };

        var analysis = BuildAnalysisWithSingleAssumption(3, 5m, 8m, "10", Serialize(detail));
        var result = _sut.Calculate(analysis, SeededBrackets);
        var values = result.MethodValues.Values.First();

        Assert.Equal(400_000m, values[0]);
        Assert.Equal(400_000m, values[1]);
        Assert.Equal(400_000m, values[2]);
    }

    [Fact]
    public void Method10_WithBrackets_FallsBackToClientTaxWhenBracketsNull()
    {
        // No brackets passed → client-supplied TotalPropertyTax should be used as-is.
        var detail = new Method10Detail
        {
            PropertyTax = new Method10Detail.PropertyTaxDetail
            {
                TotalPropertyPrice = [20_000_000m, 20_000_000m, 20_000_000m],
                TotalPropertyTax   = [100_000m,    100_000m,    100_000m]
            }
        };

        var analysis = BuildAnalysisWithSingleAssumption(3, 5m, 8m, "10", Serialize(detail));
        var result = _sut.Calculate(analysis, null);
        var values = result.MethodValues.Values.First();

        Assert.Equal(100_000m, values[0]);
        Assert.Equal(100_000m, values[1]);
        Assert.Equal(100_000m, values[2]);
    }

    [Fact]
    public void Method10_WithBrackets_FallsBackToClientTaxWhenBracketsEmpty()
    {
        var detail = new Method10Detail
        {
            PropertyTax = new Method10Detail.PropertyTaxDetail
            {
                TotalPropertyPrice = [20_000_000m],
                TotalPropertyTax   = [77_000m]
            }
        };

        var analysis = BuildAnalysisWithSingleAssumption(1, 5m, 8m, "10", Serialize(detail));
        var result = _sut.Calculate(analysis, []);
        var values = result.MethodValues.Values.First();

        Assert.Equal(77_000m, values[0]);
    }

    // Bracket boundary tests

    [Fact]
    public void DerivePropertyTax_PriceBelowLowestTier_ReturnsZero()
    {
        // 9_999_999 < 10_000_000 (lowest bracket min) → no match → 0
        var tax = IncomeCalculationService.DerivePropertyTax(9_999_999m, SeededBrackets);
        Assert.Equal(0m, tax);
    }

    [Fact]
    public void DerivePropertyTax_PriceAtTier1Min_ReturnsTier1Rate()
    {
        // Exactly 10_000_000 → Tier 1 (rate 0.02) → 200_000
        var tax = IncomeCalculationService.DerivePropertyTax(10_000_000m, SeededBrackets);
        Assert.Equal(200_000m, tax);
    }

    [Fact]
    public void DerivePropertyTax_PriceAtTier1Max_StaysInTier1()
    {
        // Exactly 50_000_000 → Tier 1 (max=50_000_000, inclusive) → 50_000_000 × 0.02 = 1_000_000
        var tax = IncomeCalculationService.DerivePropertyTax(50_000_000m, SeededBrackets);
        Assert.Equal(1_000_000m, tax);
    }

    [Fact]
    public void DerivePropertyTax_PriceAtTier2Min_UsesTier2Rate()
    {
        // 50_000_001 → Tier 2 (rate 0.03) → 50_000_001 × 0.03 = 1_500_000.03
        var tax = IncomeCalculationService.DerivePropertyTax(50_000_001m, SeededBrackets);
        Assert.Equal(50_000_001m * 0.03m, tax);
    }

    [Fact]
    public void DerivePropertyTax_PriceInTopTierNoUpperBound_UsesTopRate()
    {
        // 999_999_999_999 → Tier 4 (rate 0.10, MaxValue=null) → × 0.10
        var tax = IncomeCalculationService.DerivePropertyTax(999_999_999_999m, SeededBrackets);
        Assert.Equal(999_999_999_999m * 0.10m, tax);
    }

    [Fact]
    public void Method10_WithBrackets_Tier3Rate()
    {
        // TotalPropertyPrice = 80_000_000 → Tier 3 (rate 0.05)
        // Expected tax = 80_000_000 × 0.05 = 4_000_000
        var detail = new Method10Detail
        {
            PropertyTax = new Method10Detail.PropertyTaxDetail
            {
                TotalPropertyPrice = [80_000_000m],
                TotalPropertyTax   = [0m]
            }
        };

        var analysis = BuildAnalysisWithSingleAssumption(1, 5m, 8m, "10", Serialize(detail));
        var result = _sut.Calculate(analysis, SeededBrackets);
        var values = result.MethodValues.Values.First();

        Assert.Equal(4_000_000m, values[0]);
    }
}
