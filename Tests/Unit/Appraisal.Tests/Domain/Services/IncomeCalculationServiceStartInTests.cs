using System.Text.Json;
using Appraisal.Domain.Appraisals.Income;
using Appraisal.Domain.Appraisals.Income.MethodDetails;
using Appraisal.Domain.Services;

namespace Appraisal.Tests.Domain.Services;

/// <summary>
/// Unit tests for the <c>startIn</c> field on Method detail records.
///
/// Contract:
///   - When <c>startIn</c> is missing from JSON or set to 0 or 1 → calculation starts at year 1
///     (the default; behaves as if the feature did not exist).
///   - When <c>startIn = N</c> with N &gt; 1 → the first (N − 1) years are zero and the
///     calculation is "shifted" to begin at year N.
///
/// Two implementation patterns are exercised:
///   - "ShiftRight" methods (03, 05, 07, 09, 12, 14) compute the full step-compounded array
///     first, then call <c>ShiftRight(result, startIn − 1)</c>.
///   - "Inline zero" methods (01, 02, 04, 06, 08, 11, 13) zero <c>result[y]</c> inside the
///     year loop when <c>y &lt; startIn − 1</c>.
///
/// Backward compatibility: legacy <c>DetailJson</c> rows that pre-date the <c>startIn</c>
/// field deserialize with <c>StartIn = 0</c>; the <c>&gt; 1</c> guard in the calculator means
/// those rows behave exactly as they did before the feature was added — no migration needed.
/// </summary>
public class IncomeCalculationServiceStartInTests
{
    private readonly IncomeCalculationService _sut = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string Serialize(object detail) => JsonSerializer.Serialize(detail, JsonOpts);

    /// <summary>
    /// Builds a minimal income analysis containing a single assumption with the supplied
    /// <paramref name="detailJson"/>. The accompanying summaryDCF section is required so the
    /// DCF pipeline runs to completion.
    /// </summary>
    private static IncomeAnalysis BuildAnalysisWithSingleAssumption(
        int years,
        string methodCode,
        string detailJson)
    {
        var analysis = IncomeAnalysis.Create(
            pricingAnalysisMethodId: Guid.NewGuid(),
            templateCode: "test",
            templateName: "Test",
            totalNumberOfYears: years,
            totalNumberOfDayInYear: 365,
            capitalizeRate: 5m,
            discountedRate: 8m);

        var section = IncomeSection.Create(analysis.Id, "income", "Test Section", "positive", 1);
        var category = section.AddCategory("income", "Test Category", "positive", 1);
        category.AddAssumption("I00", "Test Assumption", "positive", 1, methodCode, detailJson);

        var summarySection = IncomeSection.Create(analysis.Id, "summaryDCF", "Summary", "empty", 2);
        analysis.ReplaceSections([section, summarySection]);

        return analysis;
    }

    // ── Backward compatibility: missing `startIn` in JSON falls back to first year ──

    /// <summary>
    /// Legacy DB rows pre-date the <c>startIn</c> field, so their <c>DetailJson</c> has no
    /// <c>startIn</c> key. Deserialization should default <c>StartIn</c> to 0; the calculator's
    /// <c>&gt; 1</c> guard must then leave the result un-shifted (full computation from year 0).
    ///
    /// Method 03 — "ShiftRight" pattern. Hand-crafted JSON (not the typed record) so the test
    /// genuinely exercises the missing-key code path rather than serializing <c>StartIn = 0</c>.
    /// </summary>
    [Fact]
    public void Method03_StartInMissingFromJson_DefaultsToFirstYear()
    {
        var detail = new Method03Detail
        {
          FirstYearAmt = 500_000m,
          IncreaseRatePct = 0m,
          IncreaseRateYrs = 1
        };
        var analysis = BuildAnalysisWithSingleAssumption(years: 4, methodCode: "03", detailJson: Serialize(detail));
        var result = _sut.Calculate(analysis);
        var values = result.MethodValues.Values.First();

        // No shift — full first-year value at y0
        Assert.Equal(500_000m, values[0]);
        Assert.Equal(500_000m, values[1]);
        Assert.Equal(500_000m, values[2]);
        Assert.Equal(500_000m, values[3]);
    }

    /// <summary>
    /// Method 14 — "ShiftRight" pattern. Worth a dedicated test because <c>Method14Detail.StartIn</c>
    /// recently switched from <c>[JsonProperty]</c> (Newtonsoft, ignored by STJ) to
    /// <c>[JsonPropertyName]</c>. This locks in the binding so a future regression cannot silently
    /// revert it.
    /// </summary>
    [Fact]
    public void Method14_StartInMissingFromJson_DefaultsToFirstYear()
    {
        var detail = new Method14Detail
        {
          FirstYearAmt = 250_000m,
          IncreaseRatePct = 4m,
          IncreaseRateYrs = 1
        };
        
        var analysis = BuildAnalysisWithSingleAssumption(years: 3, methodCode: "14", detailJson: Serialize(detail));
        var result = _sut.Calculate(analysis);
        var values = result.MethodValues.Values.First();

        // y0=250000, y1=250000×1.04=260000, y2=260000×1.04=270400 — no shift
        Assert.Equal(250_000m, values[0]);
        Assert.Equal(260_000m, values[1]);
        Assert.Equal(270_400m, values[2]);
    }

    /// <summary>
    /// Method 13 — "inline zero" pattern. With no <c>startIn</c> on the M13 detail and an M03
    /// reference paying 1_000_000/yr flat, the proportional value (10%) must populate every year
    /// starting at y0.
    /// </summary>
    [Fact]
    public void Method13_StartInMissingFromJson_DefaultsToFirstYear()
    {
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
        
        var bId = category.Assumptions.Last().Id;
        var bValues = result.MethodValues[bId];

        // 10% × 1_000_000 every year, starting at y0
        Assert.Equal(100_000m, bValues[0]);
        Assert.Equal(100_000m, bValues[1]);
        Assert.Equal(100_000m, bValues[2]);
    }

    // ── Explicit StartIn = 1 (the documented default) ───────────────────────

    /// <summary>
    /// <c>StartIn = 1</c> means "begin in year 1" — the same as the default. The <c>&gt; 1</c>
    /// guard ensures no shift occurs; the result is identical to a missing-key payload.
    /// </summary>
    [Fact]
    public void Method03_StartInOne_BehavesAsDefault()
    {
        var detail = new Method03Detail
        {
            FirstYearAmt = 500_000m,
            IncreaseRatePct = 0m,
            IncreaseRateYrs = 1,
            StartIn = 1
        };

        var analysis = BuildAnalysisWithSingleAssumption(years: 3, methodCode: "03", detailJson: Serialize(detail));
        var result = _sut.Calculate(analysis);
        var values = result.MethodValues.Values.First();

        Assert.Equal(500_000m, values[0]);
        Assert.Equal(500_000m, values[1]);
        Assert.Equal(500_000m, values[2]);
    }

    // ── StartIn > 1: ShiftRight pattern (Methods 03, 05, 07, 09, 12, 14) ────

    /// <summary>
    /// Method 03 with <c>StartIn = 2</c>: the first year is zeroed and the original year-0 value
    /// appears at index 1. Result length is preserved (the tail is truncated by <c>ShiftRight</c>).
    /// </summary>
    [Fact]
    public void Method03_StartInTwo_ShiftsResultByOneYear()
    {
        // Without shift: [500_000, 525_000, 551_250, 578_812.50]
        // After ShiftRight by 1: [0, 500_000, 525_000, 551_250]
        var detail = new Method03Detail
        {
            FirstYearAmt = 500_000m,
            IncreaseRatePct = 5m,
            IncreaseRateYrs = 1,
            StartIn = 2
        };

        var analysis = BuildAnalysisWithSingleAssumption(years: 4, methodCode: "03", detailJson: Serialize(detail));
        var result = _sut.Calculate(analysis);
        var values = result.MethodValues.Values.First();

        Assert.Equal(0m, values[0]);
        Assert.Equal(500_000m, values[1]);
        Assert.Equal(525_000m, values[2]);
        Assert.Equal(551_250m, values[3]);
    }

    /// <summary>
    /// Boundary: <c>StartIn = totalNumberOfYears</c>. Only the very last position holds the
    /// original year-0 value; everything before it is zero.
    /// </summary>
    [Fact]
    public void Method03_StartInEqualToYears_OnlyLastYearHoldsFirstYearAmt()
    {
        // 4 years, StartIn=4 → ShiftRight by 3 → [0, 0, 0, FirstYearAmt]
        var detail = new Method03Detail
        {
            FirstYearAmt = 500_000m,
            IncreaseRatePct = 0m,
            IncreaseRateYrs = 1,
            StartIn = 4
        };

        var analysis = BuildAnalysisWithSingleAssumption(years: 4, methodCode: "03", detailJson: Serialize(detail));
        var result = _sut.Calculate(analysis);
        var values = result.MethodValues.Values.First();

        Assert.Equal(0m, values[0]);
        Assert.Equal(0m, values[1]);
        Assert.Equal(0m, values[2]);
        Assert.Equal(500_000m, values[3]);
    }

    /// <summary>
    /// Boundary: <c>StartIn &gt; totalNumberOfYears</c>. The shift consumes the entire array,
    /// leaving an all-zero result. <c>ShiftRight</c> guards this with case 3
    /// (<c>shift &gt;= length</c> → all zeros).
    /// </summary>
    [Fact]
    public void Method03_StartInGreaterThanYears_AllZeros()
    {
        var detail = new Method03Detail
        {
            FirstYearAmt = 500_000m,
            IncreaseRatePct = 5m,
            IncreaseRateYrs = 1,
            StartIn = 10  // > 3 years
        };

        var analysis = BuildAnalysisWithSingleAssumption(years: 3, methodCode: "03", detailJson: Serialize(detail));
        var result = _sut.Calculate(analysis);
        var values = result.MethodValues.Values.First();

        Assert.All(values, v => Assert.Equal(0m, v));
    }

    /// <summary>
    /// Method 14 with <c>StartIn = 2</c>. Mirrors the Method 03 shift test but locks in the
    /// recently-fixed <c>[JsonPropertyName("startIn")]</c> binding end-to-end through serialize
    /// → deserialize → calculate.
    /// </summary>
    [Fact]
    public void Method14_StartInTwo_ShiftsResultByOneYear()
    {
        // Without shift: [250_000, 260_000, 270_400]
        // After ShiftRight by 1: [0, 250_000, 260_000]
        var detail = new Method14Detail
        {
            FirstYearAmt = 250_000m,
            IncreaseRatePct = 4m,
            IncreaseRateYrs = 1,
            StartIn = 2
        };

        var analysis = BuildAnalysisWithSingleAssumption(years: 3, methodCode: "14", detailJson: Serialize(detail));
        var result = _sut.Calculate(analysis);
        var values = result.MethodValues.Values.First();

        Assert.Equal(0m, values[0]);
        Assert.Equal(250_000m, values[1]);
        Assert.Equal(260_000m, values[2]);
    }

    // ── StartIn > 1: inline-zero pattern (Methods 01, 02, 04, 06, 08, 11, 13) ──

    /// <summary>
    /// Method 13 with <c>StartIn = 2</c> referencing an M03 base: y0 is zeroed in the inner
    /// loop while later years hold the proportional value. Verifies the inline-zero branch in
    /// the M13 iterative resolver as well as the field's interaction with the convergence loop.
    /// </summary>
    [Fact]
    public void Method13_StartInTwo_ZerosYearsBeforeStartIn()
    {
        var analysis = IncomeAnalysis.Create(Guid.NewGuid(), "test", "Test", 3, 365, 5m, 8m);
        var section = IncomeSection.Create(analysis.Id, "income", "Income", "positive", 1);
        var category = section.AddCategory("income", "Cat", "positive", 1);

        // Base assumption — flat 1_000_000/yr, no shift.
        var detailA = new Method03Detail { FirstYearAmt = 1_000_000m, IncreaseRatePct = 0m, IncreaseRateYrs = 1 };
        var assumptionA = category.AddAssumption("I03", "Base Income", "positive", 1, "03", Serialize(detailA));

        // Proportional assumption — 10% of A, shifted to begin in year 2.
        var detailB = new Method13Detail
        {
            ProportionPct = 10m,
            StartIn = 2,
            RefTarget = new RefTarget { Kind = "assumption", DbId = assumptionA.Id.ToString() }
        };
        category.AddAssumption("M99", "Proportional", "positive", 2, "13", Serialize(detailB));

        var summarySection = IncomeSection.Create(analysis.Id, "summaryDCF", "Summary", "empty", 2);
        analysis.ReplaceSections([section, summarySection]);

        var result = _sut.Calculate(analysis);
        var bId = category.Assumptions.Last().Id;
        var bValues = result.MethodValues[bId];

        Assert.Equal(0m, bValues[0]);
        Assert.Equal(100_000m, bValues[1]);
        Assert.Equal(100_000m, bValues[2]);
    }
}
