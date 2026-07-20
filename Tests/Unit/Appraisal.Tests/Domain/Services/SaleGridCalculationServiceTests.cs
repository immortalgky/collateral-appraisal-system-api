using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Services;

namespace Appraisal.Tests.Domain.Services;

/// <summary>
/// Unit tests for <see cref="SaleGridCalculationService"/> focused on how the resolved
/// price unit is persisted onto the method (UnitType / ValuePerUnit) and how it drives
/// final-value rounding:
///   PerSqWa / PerSqm → per-unit rate  → no rounding, ValuePerUnit populated.
///   PerUnit (or none) → whole lumpsum → floor to nearest 1,000, ValuePerUnit null.
/// </summary>
public class SaleGridCalculationServiceTests
{
    private readonly SaleGridCalculationService _sut = new();

    private static PricingAnalysisMethod BuildMethod(params (decimal Price, string? Unit)[] comparables)
    {
        var method = PricingAnalysisMethod.Create(Guid.NewGuid(), "SaleGrid");
        foreach (var (price, unit) in comparables)
        {
            var calc = method.AddCalculation(Guid.NewGuid());
            calc.SetOfferingPrice(price, unit);
            calc.SetWeight(1m, null); // equal weight; SaleGrid sums weighted adjusted values
        }
        return method;
    }

    [Fact]
    public void PerUnit_comparable_yields_lumpsum_floored_to_thousand_with_null_valuePerUnit()
    {
        var method = BuildMethod((1_234_567m, "PerUnit"));

        _sut.Recalculate(method);

        Assert.Equal("PerUnit", method.UnitType);
        Assert.Equal(1_234_000m, method.MethodValue); // floored to nearest 1,000
        Assert.Null(method.ValuePerUnit);
    }

    [Fact]
    public void PerSqWa_comparable_keeps_rate_unrounded_and_populates_valuePerUnit()
    {
        var method = BuildMethod((12_345m, "PerSqWa"));

        _sut.Recalculate(method);

        Assert.Equal("PerSqWa", method.UnitType);
        Assert.Equal(12_345m, method.MethodValue); // per-unit rate: not rounded
        Assert.Equal(12_345m, method.ValuePerUnit);
    }

    [Fact]
    public void PerSqm_comparable_keeps_rate_unrounded_and_populates_valuePerUnit()
    {
        var method = BuildMethod((6_789m, "PerSqm"));

        _sut.Recalculate(method);

        Assert.Equal("PerSqm", method.UnitType);
        Assert.Equal(6_789m, method.MethodValue);
        Assert.Equal(6_789m, method.ValuePerUnit);
    }

    [Fact]
    public void Missing_unit_defaults_to_lumpsum_perUnit()
    {
        var method = BuildMethod((5_555m, null));

        _sut.Recalculate(method);

        Assert.Equal("PerUnit", method.UnitType);
        Assert.Equal(5_000m, method.MethodValue); // floored
        Assert.Null(method.ValuePerUnit);
    }

    [Fact]
    public void Dominant_unit_wins_across_comparables()
    {
        // Two PerUnit rows vs one PerSqWa → dominant is PerUnit → lumpsum rounding.
        var method = BuildMethod(
            (1_000_500m, "PerUnit"),
            (2_000_500m, "PerUnit"),
            (3_000m, "PerSqWa"));

        _sut.Recalculate(method);

        Assert.Equal("PerUnit", method.UnitType);
        // sum = 1,000,500 + 2,000,500 + 3,000 = 3,004,000 → already on a 1,000 boundary
        Assert.Equal(3_004_000m, method.MethodValue);
        Assert.Null(method.ValuePerUnit);
    }
}
