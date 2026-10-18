using Appraisal.Domain.Projects;

namespace Appraisal.Tests.Domain;

public class ProjectUnitPriceImportDomainTests
{
    [Fact]
    public void ApplyImportedValues_StoresFileValuesAsIs_AndClearsCalculatorState()
    {
        var price = ProjectUnitPrice.Create(Guid.NewGuid());
        price.UpdateCondoLocationFlags(true, true, true, true, true);
        price.UpdateCondoCalculatedValues(10m, 20m, 30m, 40m, 50m, 60m, 70m);

        price.ApplyImportedValues(41_000m, 1_234_567.89m, 900_000m, 55_500m);

        Assert.Equal(41_000m, price.StandardPrice);
        Assert.Equal(1_234_567.89m, price.TotalAppraisalValue);
        Assert.Equal(1_234_567.89m, price.TotalAppraisalValueRounded);
        Assert.Equal(900_000m, price.ForceSellingPrice);
        Assert.Equal(55_500m, price.CoverageAmount);
        Assert.False(price.IsCorner || price.IsEdge || price.IsOther || price.IsPoolView || price.IsSouth || price.IsNearGarden);
        Assert.Null(price.AdjustPriceLocation);
        Assert.Null(price.PriceIncrementPerFloor);
        Assert.Null(price.LandIncreaseDecreaseAmount);
    }

    [Fact]
    public void ApplyImportedValues_ClearsLandAndBuildingAdjustments()
    {
        var price = ProjectUnitPrice.Create(Guid.NewGuid());
        price.UpdateLandAndBuildingLocationFlags(true, false, true, false);
        price.UpdateLandAndBuildingCalculatedValues(5m, 6m, 7m, 8m, 9m, 10m, 11m);

        price.ApplyImportedValues(1m, 2m, 3m, 4m);

        Assert.False(price.IsCorner || price.IsNearGarden);
        Assert.Null(price.LandIncreaseDecreaseAmount);
        Assert.Null(price.AdjustPriceLocation);
    }

    [Fact]
    public void ClearImportedValues_EmptiesTheFourPrices()
    {
        var price = ProjectUnitPrice.Create(Guid.NewGuid());
        price.ApplyImportedValues(1m, 2m, 3m, 4m);

        price.ClearImportedValues();

        Assert.Null(price.StandardPrice);
        Assert.Null(price.TotalAppraisalValue);
        Assert.Null(price.TotalAppraisalValueRounded);
        Assert.Null(price.ForceSellingPrice);
        Assert.Null(price.CoverageAmount);
    }

    [Fact]
    public void Project_DefaultsToCalculated_ImportRecordsMetadata_CalculateKeepsIt()
    {
        var project = Project.Create(Guid.NewGuid(), ProjectType.Condo);
        Assert.Equal(UnitPriceSource.Calculated, project.UnitPriceSource);

        var at = new DateTime(2026, 10, 16, 14, 30, 0);
        project.MarkUnitPricesImported("prices.xlsx", at, "somchai");

        Assert.Equal(UnitPriceSource.Imported, project.UnitPriceSource);
        Assert.Equal("prices.xlsx", project.UnitPriceImportFileName);
        Assert.Equal(at, project.UnitPriceImportedAt);
        Assert.Equal("somchai", project.UnitPriceImportedBy);

        project.MarkUnitPricesCalculated();

        Assert.Equal(UnitPriceSource.Calculated, project.UnitPriceSource);
        Assert.Equal("prices.xlsx", project.UnitPriceImportFileName);
        Assert.Equal("somchai", project.UnitPriceImportedBy);
    }

    [Fact]
    public void ReplaceUnits_ClearsImportMetadata_ButKeepsTheSource()
    {
        var project = Project.Create(Guid.NewGuid(), ProjectType.Condo);
        project.MarkUnitPricesImported("prices.xlsx", new DateTime(2026, 10, 16), "somchai");

        project.ReplaceUnits("units.xlsx", null, []);

        Assert.Equal(UnitPriceSource.Imported, project.UnitPriceSource);
        Assert.Null(project.UnitPriceImportFileName);
        Assert.Null(project.UnitPriceImportedAt);
        Assert.Null(project.UnitPriceImportedBy);
    }

    [Fact]
    public void CalculateUnitPrices_AlwaysSetsForceSellingPrice_FromTheSuppliedPercentage()
    {
        var project = Project.Create(Guid.NewGuid(), ProjectType.Condo);
        // The assumption leaves the force-sale % blank: the supplied (resolved) one must still apply.
        project.SetCondoPricingAssumption(null, null, null, null, null, null, null, null, forceSalePercentage: null);
        project.ImportUnits("units.xlsx", null,
            [ProjectUnit.CreateCondo(project.Id, Guid.Empty, 1, floor: 1, towerName: "A", roomNumber: "101",
                modelType: "T1", usableArea: 10m)]);
        var modelId = project.Models.Single().Id;

        var prices = project.CalculateUnitPrices(
            new Dictionary<Guid, ProjectUnitPrice>(),
            new Dictionary<Guid, decimal?> { [modelId] = 40_000m },
            new Dictionary<string, decimal>(),
            forceSalePercentage: 70m);

        var price = Assert.Single(prices);
        Assert.Equal(400_000m, price.TotalAppraisalValueRounded);
        Assert.Equal(280_000m, price.ForceSellingPrice);
    }

    [Theory]
    [InlineData(null, 70)]   // blank assumption -> the system default
    [InlineData(65, 65)]
    [InlineData(0, 0)]       // 0% is a real value
    [InlineData(100, 100)]
    public void EffectiveForceSalePercentage_IsTheAssumptionsOwn_ElseTheSystemDefault(int? assumed, int expected)
    {
        var project = Project.Create(Guid.NewGuid(), ProjectType.Condo);
        project.SetCondoPricingAssumption(null, null, null, null, null, null, null, null, assumed);

        Assert.Equal(expected, project.EffectiveForceSalePercentage(70m));
    }

    [Theory]
    [InlineData(55, 55)]
    [InlineData(0, 0)]
    [InlineData(100, 100)]
    [InlineData(101, 70)]    // a bad system default -> 70
    [InlineData(-1, 70)]
    [InlineData(null, 70)]   // missing -> 70
    public void SystemDefaultOrFallback_KeepsOnlyAValueWithin0To100(int? configured, int expected) =>
        Assert.Equal(expected, Project.SystemDefaultOrFallback(configured));

    [Fact]
    public void ResolveForceSalePercentage_FallsThroughOwnThenDefaultThen70()
    {
        Assert.Equal(60m, Project.ResolveForceSalePercentage(60m, 55m));
        Assert.Equal(55m, Project.ResolveForceSalePercentage(null, 55m));
        Assert.Equal(55m, Project.ResolveForceSalePercentage(250m, 55m));
        Assert.Equal(70m, Project.ResolveForceSalePercentage(null, 250m));
        Assert.Equal(0m, Project.ResolveForceSalePercentage(0m, 55m));
    }

    [Fact]
    public void EffectiveForceSalePercentage_WithoutAnAssumption_IsTheSystemDefault()
    {
        var project = Project.Create(Guid.NewGuid(), ProjectType.Condo);

        Assert.Equal(55m, project.EffectiveForceSalePercentage(55m));
    }

    [Fact]
    public void CalculateUnitPrices_AZeroPercent_GivesAZeroForceSellingPrice_NotNull()
    {
        var project = Project.Create(Guid.NewGuid(), ProjectType.Condo);
        project.SetCondoPricingAssumption(null, null, null, null, null, null, null, null, forceSalePercentage: 0m);
        project.ImportUnits("units.xlsx", null,
            [ProjectUnit.CreateCondo(project.Id, Guid.Empty, 1, floor: 1, towerName: "A", roomNumber: "101",
                modelType: "T1", usableArea: 10m)]);
        var modelId = project.Models.Single().Id;

        var price = Assert.Single(project.CalculateUnitPrices(
            new Dictionary<Guid, ProjectUnitPrice>(),
            new Dictionary<Guid, decimal?> { [modelId] = 40_000m },
            new Dictionary<string, decimal>(),
            forceSalePercentage: project.EffectiveForceSalePercentage(70m)));

        Assert.Equal(400_000m, price.TotalAppraisalValueRounded);
        Assert.Equal(0m, price.ForceSellingPrice);
    }

    // The three half-baht cases the FE live preview (recomputeUnitPrice.test.ts) pins with the same numbers:
    // the backend is decimal, the FE is double snapped to 6 dp, and both must land on the same baht.
    [Fact]
    public void Calculate_Condo_50250_x_20_15_RoundsHalfAwayFromZero_To_1012538()
    {
        var project = Project.Create(Guid.NewGuid(), ProjectType.Condo);
        project.SetCondoPricingAssumption(null, null, null, null, null, null, null, null, forceSalePercentage: 70m);
        project.ImportUnits("units.xlsx", null,
            [ProjectUnit.CreateCondo(project.Id, Guid.Empty, 1, floor: 1, towerName: "A", roomNumber: "101",
                modelType: "T1", usableArea: 20.15m)]);

        var price = Assert.Single(project.CalculateUnitPrices(
            new Dictionary<Guid, ProjectUnitPrice>(),
            new Dictionary<Guid, decimal?> { [project.Models.Single().Id] = 50_250m },
            new Dictionary<string, decimal>(),
            forceSalePercentage: 70m));

        Assert.Equal(1_012_538m, price.TotalAppraisalValueRounded);
    }

    [Fact]
    public void Calculate_LandAndBuilding_Land50_05_Vs_50_At100000_On3000000_Is3010000()
    {
        var project = Project.Create(Guid.NewGuid(), ProjectType.LandAndBuilding);
        project.SetLandAndBuildingPricingAssumption(null, null, null, null, null, 100_000m, 70m);
        project.ImportUnits("units.xlsx", null,
        [
            ProjectUnit.CreateLandAndBuilding(project.Id, Guid.Empty, 1, "1", "99/1", "H1", 2, 50.00m, 120m, 1m),
            ProjectUnit.CreateLandAndBuilding(project.Id, Guid.Empty, 2, "2", "99/2", "H1", 2, 50.05m, 120m, 1m)
        ]);
        var model = project.Models.Single();
        Assert.Equal(50.00m, model.StandardLandArea);

        var prices = project.CalculateUnitPrices(
            new Dictionary<Guid, ProjectUnitPrice>(),
            new Dictionary<Guid, decimal?> { [model.Id] = 3_000_000m },
            new Dictionary<string, decimal>(),
            forceSalePercentage: 70m);

        var wider = prices.Single(p => p.LandIncreaseDecreaseAmount != 0m);
        Assert.Equal(5_000m, wider.LandIncreaseDecreaseAmount);
        Assert.Equal(3_010_000m, wider.TotalAppraisalValueRounded);
    }

    [Fact]
    public void Calculate_ForcedSale_1500000_x_70_1_Percent_Is_1052000()
    {
        var project = Project.Create(Guid.NewGuid(), ProjectType.Condo);
        project.SetCondoPricingAssumption(null, null, null, null, null, null, null, null, forceSalePercentage: 70.1m);
        project.ImportUnits("units.xlsx", null,
            [ProjectUnit.CreateCondo(project.Id, Guid.Empty, 1, floor: 1, towerName: "A", roomNumber: "101",
                modelType: "T1", usableArea: 1m)]);

        var price = Assert.Single(project.CalculateUnitPrices(
            new Dictionary<Guid, ProjectUnitPrice>(),
            new Dictionary<Guid, decimal?> { [project.Models.Single().Id] = 1_500_000m },
            new Dictionary<string, decimal>(),
            forceSalePercentage: 70.1m));

        Assert.Equal(1_500_000m, price.TotalAppraisalValueRounded);
        Assert.Equal(1_052_000m, price.ForceSellingPrice);   // 1,051,500 rounds half away from zero
    }
}
