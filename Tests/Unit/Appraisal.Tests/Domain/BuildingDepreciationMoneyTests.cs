using Appraisal.Domain.Appraisals;

namespace Appraisal.Tests.Domain;

/// <summary>
/// Each depreciation column holds the figure the database will store — money at 2 places, area and
/// percentages at 4 — rounded half away from zero as SQL Server does, so a data correction does not log
/// a sub-satang "change" (7754.93 → 7754.9325) for a value that is never written differently.
/// </summary>
public class BuildingDepreciationMoneyTests
{
    [Fact]
    public void Create_holds_every_money_figure_at_two_places()
    {
        var row = BuildingDepreciationDetail.Create(
            Guid.NewGuid(), "Gross",
            pricePerSqMBeforeDepreciation: 9_123.455m,
            priceBeforeDepreciation: 1_368_517.505m,
            pricePerSqMAfterDepreciation: 7_754.9325m,
            priceAfterDepreciation: 1_163_239.875m,
            priceDepreciation: 205_277.625m);

        Assert.Equal(9_123.46m, row.PricePerSqMBeforeDepreciation);
        Assert.Equal(1_368_517.51m, row.PriceBeforeDepreciation);
        Assert.Equal(7_754.93m, row.PricePerSqMAfterDepreciation);
        Assert.Equal(1_163_239.88m, row.PriceAfterDepreciation);
        Assert.Equal(205_277.63m, row.PriceDepreciation); // banker's rounding would give .62
    }

    [Fact]
    public void Update_and_periods_round_the_same_way()
    {
        var row = BuildingDepreciationDetail.Create(Guid.NewGuid(), "Period");

        row.Update("Period", pricePerSqMAfterDepreciation: 8_211.105m, priceDepreciation: 136_851.745m);
        var period = row.AddPeriod(1, 5, 2m, 10m, 205_277.625m);

        Assert.Equal(8_211.11m, row.PricePerSqMAfterDepreciation);
        Assert.Equal(136_851.75m, row.PriceDepreciation);
        Assert.Equal(205_277.63m, period.PriceDepreciation);
    }

    [Fact]
    public void Percentages_and_area_are_held_at_their_four_places()
    {
        // 3 × 1.1 in floating point is 3.3000000000000003; the column stores 3.3000.
        var row = BuildingDepreciationDetail.Create(
            Guid.NewGuid(), "Gross",
            area: 150.12345m, depreciationYearPct: 1.1m, totalDepreciationPct: 3.3000000000000003m);
        var period = row.AddPeriod(1, 3, 1.10000000000000009m, 3.3000000000000003m, 0m);

        Assert.Equal(150.1235m, row.Area);
        Assert.Equal(1.1m, row.DepreciationYearPct);
        Assert.Equal(3.3m, row.TotalDepreciationPct);
        Assert.Equal(1.1m, period.DepreciationPerYear);
        Assert.Equal(3.3m, period.TotalDepreciationPct);
    }

    [Fact]
    public void A_whole_figure_carries_the_column_scale_so_the_audit_prints_it_like_the_stored_one()
    {
        var row = BuildingDepreciationDetail.Create(Guid.NewGuid(), "Gross", priceDepreciation: 5000m, totalDepreciationPct: 1.2m);

        Assert.Equal("5000.00", row.PriceDepreciation.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("1.2000", row.TotalDepreciationPct.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Float_noise_below_zero_is_held_as_zero_not_negative_zero()
    {
        var row = BuildingDepreciationDetail.Create(Guid.NewGuid(), "Gross", priceAfterDepreciation: -1.4551915228366852E-10m);

        Assert.Equal("0.00", row.PriceAfterDepreciation.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
