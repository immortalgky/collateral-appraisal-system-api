using System.Reflection;
using Appraisal.Domain.Projects;

namespace Appraisal.Tests.Domain;

/// <summary>
/// Land has no model standard price, so "AdjustPricePercentage" applies to the land value
/// (landArea x LandIncreaseDecreaseRate). LandAndBuilding keeps using the model's standard price.
/// </summary>
public class LandUnitPriceTests
{
    private static readonly IReadOnlyDictionary<string, decimal> NoRates = new Dictionary<string, decimal>();

    private static ProjectUnitPrice CalculateLand(string locationMethod, decimal cornerAdjustment)
    {
        var project = Project.Create(Guid.NewGuid(), ProjectType.Land);
        project.SetLandAndBuildingPricingAssumption(
            locationMethod, cornerAdjustment, null, null, null, landIncreaseDecreaseRate: 20_000m, forceSalePercentage: 70m);
        project.ImportUnits("units.xlsx", null,
            [ProjectUnit.CreateLandAndBuilding(project.Id, Guid.Empty, 1, plotNumber: "P1", landArea: 50m)]);
        var unit = project.Units.Single();

        var existing = ProjectUnitPrice.Create(unit.Id);
        existing.UpdateLandAndBuildingLocationFlags(isCorner: true, isEdge: false, isNearGarden: false, isOther: false);

        var prices = project.CalculateUnitPrices(
            new Dictionary<Guid, ProjectUnitPrice> { [unit.Id] = existing },
            new Dictionary<Guid, decimal?>(), NoRates, forceSalePercentage: 70m);
        return Assert.Single(prices);
    }

    [Fact]
    public void Land_Percentage_IsAppliedToTheLandValue()
    {
        var price = CalculateLand("AdjustPricePercentage", cornerAdjustment: 5m);

        // land value 50 x 20,000 = 1,000,000; 5% of it = 50,000
        Assert.Equal(1_000_000m, price.LandIncreaseDecreaseAmount);
        Assert.Equal(5m, price.AdjustPriceLocation);   // stored raw, scaled only inside the total
        Assert.Equal(1_050_000m, price.TotalAppraisalValue);
        Assert.Equal(1_050_000m, price.TotalAppraisalValueRounded);
        Assert.Equal(735_000m, price.ForceSellingPrice);
    }

    [Fact]
    public void Land_PerSqm_ScalesByLandArea()
    {
        var price = CalculateLand("AdjustPriceSqm", cornerAdjustment: 120m);

        // 120 x 50 sq.wa = 6,000 on top of the 1,000,000 land value, rounded to the nearest 10,000
        Assert.Equal(1_006_000m, price.TotalAppraisalValue);
        Assert.Equal(1_010_000m, price.TotalAppraisalValueRounded);
    }

    [Fact]
    public void Land_IgnoresModelsLeftOverFromBeforeLandHadNone()
    {
        var project = Project.Create(Guid.NewGuid(), ProjectType.Land);
        project.SetLandAndBuildingPricingAssumption(
            "AdjustPricePercentage", 5m, null, null, null, landIncreaseDecreaseRate: 20_000m, forceSalePercentage: 70m);
        project.ImportUnits("units.xlsx", null,
            [ProjectUnit.CreateLandAndBuilding(project.Id, Guid.Empty, 1, plotNumber: "P1", modelType: "A", landArea: 50m)]);
        var unit = project.Units.Single();
        // Land can no longer create models, so plant the one an old L project still has
        var model = ProjectModel.Create(project.Id, "A");
        ((List<ProjectModel>)typeof(Project).GetField("_models", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(project)!).Add(model);
        var modelId = model.Id;

        var existing = ProjectUnitPrice.Create(unit.Id);
        existing.UpdateLandAndBuildingLocationFlags(isCorner: true, isEdge: false, isNearGarden: false, isOther: false);

        var price = Assert.Single(project.CalculateUnitPrices(
            new Dictionary<Guid, ProjectUnitPrice> { [unit.Id] = existing },
            new Dictionary<Guid, decimal?> { [modelId] = 2_000_000m }, NoRates, forceSalePercentage: 70m));

        // landArea x rate + 5% of it; the model's 2,000,000 standard price never enters
        Assert.Equal(0m, price.StandardPrice);
        Assert.Equal(1_000_000m, price.LandIncreaseDecreaseAmount);
        Assert.Equal(1_050_000m, price.TotalAppraisalValueRounded);
    }

    [Fact]
    public void LandAndBuilding_Percentage_StillUsesTheModelStandardPrice()
    {
        var project = Project.Create(Guid.NewGuid(), ProjectType.LandAndBuilding);
        project.SetLandAndBuildingPricingAssumption(
            "AdjustPricePercentage", 5m, null, null, null, landIncreaseDecreaseRate: 0m, forceSalePercentage: 70m);
        project.ImportUnits("units.xlsx", null,
            [ProjectUnit.CreateLandAndBuilding(project.Id, Guid.Empty, 1, plotNumber: "P1", modelType: "H1", landArea: 50m)]);
        var unit = project.Units.Single();
        var modelId = project.Models.Single().Id;

        var existing = ProjectUnitPrice.Create(unit.Id);
        existing.UpdateLandAndBuildingLocationFlags(isCorner: true, isEdge: false, isNearGarden: false, isOther: false);

        var price = Assert.Single(project.CalculateUnitPrices(
            new Dictionary<Guid, ProjectUnitPrice> { [unit.Id] = existing },
            new Dictionary<Guid, decimal?> { [modelId] = 2_000_000m }, NoRates, forceSalePercentage: 70m));

        // 5% of the model's 2,000,000 standard price, not of anything land-based
        Assert.Equal(5m, price.AdjustPriceLocation);
        Assert.Equal(2_000_000m, price.StandardPrice);
        Assert.Equal(2_100_000m, price.TotalAppraisalValueRounded);
    }
}
