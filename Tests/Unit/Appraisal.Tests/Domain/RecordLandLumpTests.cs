using Appraisal.Domain.Appraisals;
using Shared.Exceptions;

namespace Appraisal.Tests.Domain;

public class RecordLandLumpTests
{
    private static PricingAnalysisMethod NewLandMethod()
    {
        var pa = PricingAnalysis.CreateForPropertyGroup(Guid.NewGuid());
        var approach = pa.AddApproach("Cost");
        var method = approach.AddMethod("WQS");
        approach.SetMethodRole(method.Id, "Land");
        return method;
    }

    [Fact]
    public void Creates_the_row_with_the_lump_as_final_indicated_and_land_value()
    {
        var method = NewLandMethod();
        method.SetValue(5_000_000m, null, "PerUnit");

        var row = method.RecordLandLump(5_000_000m, 510m);

        Assert.Same(row, method.FinalValue);
        Assert.Equal(5_000_000m, row.FinalValue);
        Assert.Equal(5_000_000m, row.IndicatedValue);
        Assert.Equal(5_000_000m, row.LandValue);
        Assert.Equal(510m, row.LandArea);
        Assert.True(row.IncludeLandArea);
        Assert.Null(row.FinalValueOverride);
        Assert.False(row.HasBuildingValue);
        Assert.Equal("PerUnit", row.FinalValueUnitType);
    }

    [Fact]
    public void Reuses_the_row_and_clears_its_override_building_and_exclusion()
    {
        var method = NewLandMethod();
        var existing = PricingFinalValue.Create(method.Id, 1m);
        method.SetFinalValue(existing);
        existing.SetFinalValueOverride(85_500m);
        existing.SetBuildingValue(2_000_000m);
        existing.ExcludeLandArea();

        var row = method.RecordLandLump(5_000_000m, 510m);

        Assert.Same(existing, row);
        Assert.Null(row.FinalValueOverride);
        Assert.False(row.HasBuildingValue);
        Assert.Null(row.BuildingValue);
        Assert.True(row.IncludeLandArea);
        Assert.Equal(510m, row.LandArea);
        Assert.Equal(5_000_000m, row.LandValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_non_positive_area_is_rejected(int area)
    {
        Assert.Throws<DomainException>(() => NewLandMethod().RecordLandLump(5_000_000m, area));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_lump_of_zero_or_less_is_rejected(int lump)
    {
        Assert.Throws<DomainException>(() => NewLandMethod().RecordLandLump(lump, 510m));
    }
}
