using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Services;
using Parameter.Contracts.PricingParameters;

namespace Appraisal.Tests.Domain.Services;

/// <summary>
/// Pins the rounding rules the 2026-09-23/24 fixes settled, each of which existed because this side
/// and the screen disagreed about the same figure.
/// <para>
/// The recurring trap: C# <c>Math.Round(x, n)</c> with no <see cref="MidpointRounding"/> is banker's
/// rounding, and on <c>decimal</c> the midpoints are exact so the even-rule really fires — while the
/// screen's helpers (<c>round2</c>, <c>roundToThousand</c>, <c>toDecimal</c>) all round halves up.
/// Every rounding on a money path therefore names its mode.
/// </para>
/// </summary>
public class PricingRoundingTests
{
    // ── satang, for the SaleGrid / DirectComparison intermediates ────────────────────────────────
    // The screen rounds every step (calculateSaleAdjustmentGrid.ts round2); this side rounded none,
    // so reopening a saved job re-seeded the grid with satang the appraiser never typed.

    [Theory]
    [InlineData(19999.804, 19999.80)]
    [InlineData(28233.051, 28233.05)]
    [InlineData(0.125, 0.13)]    // banker's would give 0.12
    [InlineData(1234.565, 1234.57)] // banker's would give 1234.56
    [InlineData(-0.125, -0.13)]  // away from zero, not toward it
    public void Round2_rounds_halves_away_from_zero(decimal input, decimal expected)
        => Assert.Equal(expected, PricingCalculationHelper.Round2(input));

    // ── nearest thousand, for a whole-unit lumpsum ───────────────────────────────────────────────
    // This used to floor, so a saved value could sit up to 999 baht below the figure on screen.

    [Theory]
    [InlineData(7_279_927.20, 7_280_000)] // floor gave 7,279,000 — a full 1,000 low
    [InlineData(1_234_567, 1_235_000)]
    [InlineData(1_234_400, 1_234_000)]
    [InlineData(1_500, 2_000)]            // banker's would give 2,000 here too, but not at 2,500
    [InlineData(2_500, 3_000)]            // banker's would give 2,000
    public void Lumpsum_rounds_to_the_nearest_thousand(decimal input, decimal expected)
    {
        // No unit on the comparables → lumpsum, which is the branch that rounds.
        var calcs = Array.Empty<PricingCalculation>();
        Assert.Equal(expected, PricingCalculationHelper.RoundFinalValue(input, calcs));
    }

    [Fact]
    public void A_per_area_rate_is_never_rounded_to_a_thousand()
    {
        var method = PricingAnalysisMethodStub.WithSellingUnit("PerSqWa");
        Assert.Equal(71_565.95m, PricingCalculationHelper.RoundFinalValue(71_565.95m, method));
    }

    // ── whole baht, for the property tax a human reads off the Method 10 row ─────────────────────

    [Fact]
    public void Property_tax_rounds_a_half_baht_up_not_to_the_even_baht()
    {
        // One bracket at 0.5%: a price of 500 lands the tax exactly on 2.5.
        var brackets = new List<TaxBracketDto> { new(Tier: 1, TaxRate: 0.005m, MinValue: 0m, MaxValue: null) };

        // 2.5 → 3 under AwayFromZero; banker's would give 2, one baht below the screen.
        Assert.Equal(3m, IncomeCalculationService.DerivePropertyTax(500m, brackets));
    }

    [Fact]
    public void Property_tax_returns_zero_when_the_price_sits_below_every_bracket()
    {
        var brackets = new List<TaxBracketDto> { new(Tier: 1, TaxRate: 0.005m, MinValue: 1_000m, MaxValue: null) };
        Assert.Equal(0m, IncomeCalculationService.DerivePropertyTax(500m, brackets));
    }
}

/// <summary>Builds the smallest comparable list that carries a price unit.</summary>
internal static class PricingAnalysisMethodStub
{
    public static IEnumerable<PricingCalculation> WithSellingUnit(string unit)
    {
        var calc = PricingCalculation.Create(Guid.NewGuid(), Guid.NewGuid());
        calc.SetSellingPrice(1m, unit);
        return new[] { calc };
    }
}
