using Appraisal.Domain.Appraisals;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Appraisal.Tests.Domain;

/// <summary>
/// BuildingCostValue holds what the screen shows: the appraiser's typed value, otherwise the depreciated
/// value of EVERY row (Non-Building included), each at 2 decimals, rounded to the nearest 1,000.
/// </summary>
public class BuildingCostValueTests
{
    private static BuildingAppraisalDetail NewBuilding()
        => AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now)
            .AddBuildingProperty().BuildingDetail!;

    [Fact]
    public void A_typed_value_wins_over_the_computed_one()
    {
        var building = NewBuilding();
        building.AddDepreciationDetail("Gross", priceAfterDepreciation: 700_000m);

        building.Update(buildingCostValue: 650_000m);
        building.ResolveDerivedValues();

        Assert.Equal(650_000m, building.BuildingCostValue);
    }

    [Fact]
    public void Without_a_typed_value_it_is_computed_from_every_row_including_non_building_ones()
    {
        var building = NewBuilding();
        building.AddDepreciationDetail("Gross", isBuilding: true, priceAfterDepreciation: 700_000m);
        building.AddDepreciationDetail("Gross", isBuilding: false, priceAfterDepreciation: 120_400m);

        building.Update(buildingCostValue: null);
        building.ResolveDerivedValues();

        Assert.Equal(820_000m, building.BuildingCostValue);
        Assert.Equal(700_000m, building.BuildingInsurancePrice); // insurance still counts IsBuilding rows only
    }

    [Theory]
    [InlineData(250_400, 250_000)]
    [InlineData(250_500, 251_000)] // half away from zero, like SQL ROUND(x, -3); banker's would give 250,000
    [InlineData(251_500, 252_000)]
    public void The_sum_is_rounded_to_the_nearest_thousand_half_away_from_zero(decimal price, decimal expected)
    {
        var building = NewBuilding();
        building.AddDepreciationDetail("Gross", priceAfterDepreciation: price);

        building.ResolveDerivedValues();

        Assert.Equal(expected, building.BuildingCostValue);
    }

    [Fact]
    public void Each_row_is_taken_at_the_two_decimals_the_column_stores()
    {
        var building = NewBuilding();
        // Unsaved rows can carry more decimals; saved, each is 141,750.00, so the sum is 283,500 -> 284,000.
        building.AddDepreciationDetail("Gross", priceAfterDepreciation: 141_749.996m);
        building.AddDepreciationDetail("Gross", priceAfterDepreciation: 141_749.996m);

        building.ResolveDerivedValues();

        Assert.Equal(284_000m, building.BuildingCostValue);
    }

    [Fact]
    public void With_no_rows_it_stays_not_entered()
    {
        var building = NewBuilding();

        building.ResolveDerivedValues();

        Assert.Null(building.BuildingCostValue);
    }

    [Fact]
    public void Resolving_again_keeps_a_stored_value()
    {
        var building = NewBuilding();
        var row = building.AddDepreciationDetail("Gross", priceAfterDepreciation: 250_400m);
        building.ResolveDerivedValues();
        Assert.Equal(250_000m, building.BuildingCostValue);

        // The caller re-sends null on every save (Update), so the value follows the changed row...
        row.Update("Gross", null, 0m, 0, true, 0m, 0m, 0m, 312_600m, 0m, 0m, 0m);
        building.Update(buildingCostValue: null);
        building.ResolveDerivedValues();
        Assert.Equal(313_000m, building.BuildingCostValue);

        // ...but ResolveDerivedValues alone never overwrites what is already stored.
        row.Update("Gross", null, 0m, 0, true, 0m, 0m, 0m, 1_000m, 0m, 0m, 0m);
        building.ResolveDerivedValues();
        Assert.Equal(313_000m, building.BuildingCostValue);
    }
}
