using Appraisal.Domain.Appraisals;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Appraisal.Tests.Domain;

/// <summary>
/// BuildingInsurancePrice holds what the screen shows: the appraiser's typed value, otherwise the value
/// computed from the IsBuilding depreciation rows, rounded to the nearest 1,000 like SQL ROUND(x, -3).
/// </summary>
public class BuildingInsurancePriceTests
{
    private static BuildingAppraisalDetail NewBuilding()
        => AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now)
            .AddBuildingProperty().BuildingDetail!;

    [Fact]
    public void A_typed_value_wins_over_the_computed_one()
    {
        var building = NewBuilding();
        building.AddDepreciationDetail("Gross", isBuilding: true, priceAfterDepreciation: 700_000m);

        building.Update(buildingInsurancePrice: 400_000m);
        building.ResolveDerivedValues();

        Assert.Equal(400_000m, building.BuildingInsurancePrice);
    }

    [Theory]
    [InlineData(250_400, 250_000)]
    [InlineData(250_499.99, 250_000)]
    [InlineData(250_500, 251_000)] // half away from zero, like SQL ROUND(x, -3); banker's would give 250,000
    [InlineData(251_500, 252_000)]
    public void Without_a_typed_value_it_is_computed_from_the_building_rows_and_rounded_to_the_thousand(
        decimal priceAfterDepreciation, decimal expected)
    {
        var building = NewBuilding();
        building.AddDepreciationDetail("Gross", isBuilding: true, priceAfterDepreciation: priceAfterDepreciation);
        // Not insurable: must be left out of the sum.
        building.AddDepreciationDetail("Gross", isBuilding: false, priceAfterDepreciation: 999_999m);

        building.Update(buildingInsurancePrice: null);
        building.ResolveDerivedValues();

        Assert.Equal(expected, building.BuildingInsurancePrice);
    }

    [Fact]
    public void The_rounding_is_applied_to_the_sum_of_the_rows_not_to_each_row()
    {
        var building = NewBuilding();
        building.AddDepreciationDetail("Gross", isBuilding: true, priceAfterDepreciation: 100_400m);
        building.AddDepreciationDetail("Gross", isBuilding: true, priceAfterDepreciation: 100_400m);

        building.ResolveDerivedValues();

        Assert.Equal(201_000m, building.BuildingInsurancePrice); // 200,800 -> 201,000 (per row would be 200,000)
    }

    [Fact]
    public void Each_row_is_taken_at_the_two_decimals_the_column_stores()
    {
        var building = NewBuilding();
        // Unsaved rows can carry more decimals; saved, each is 141,750.00, so the sum is 283,500 -> 284,000.
        building.AddDepreciationDetail("Gross", isBuilding: true, priceAfterDepreciation: 141_749.996m);
        building.AddDepreciationDetail("Gross", isBuilding: true, priceAfterDepreciation: 141_749.996m);

        building.ResolveDerivedValues();

        Assert.Equal(284_000m, building.BuildingInsurancePrice);
    }

    [Fact]
    public void With_no_building_rows_it_stays_not_entered()
    {
        var building = NewBuilding();
        building.AddDepreciationDetail("Gross", isBuilding: false, priceAfterDepreciation: 500_000m);

        building.ResolveDerivedValues();

        Assert.Null(building.BuildingInsurancePrice);
    }
}
