using Reporting.Application.Providers;

namespace Reporting.Tests;

/// <summary>
/// The book prints a DERIVED ราคาต่อหน่วย (land value / printed area) for a Role=Land method priced as
/// a lump (ราคาเหมา). Display only: nothing stores it. There is no harness for the provider's
/// row-building (it sits on Dapper + SQL), so the one pure piece — the division — is pinned here.
/// </summary>
public class DerivedUnitPriceTests
{
    [Fact]
    public void Appraisal_69105533_prints_5882_35_per_sqwa()
    {
        Assert.Equal(5_882.35m, AppraisalSummaryLandBuildingDataProvider.DerivedUnitPrice(3_000_000m, 510m));
    }

    [Fact]
    public void Rounds_to_two_decimals_away_from_zero()
    {
        // 1 / 8 = 0.125 -> 0.13
        Assert.Equal(0.13m, AppraisalSummaryLandBuildingDataProvider.DerivedUnitPrice(1m, 8m));
    }

    [Theory]
    [InlineData(null, 510)]
    [InlineData(0, 510)]
    [InlineData(-5, 510)]
    [InlineData(3_000_000, 0)]
    public void Is_null_without_a_positive_value_and_area(int? landValue, int area)
    {
        Assert.Null(AppraisalSummaryLandBuildingDataProvider.DerivedUnitPrice(landValue, area));
    }
}
