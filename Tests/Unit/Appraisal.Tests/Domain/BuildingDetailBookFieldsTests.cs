using Appraisal.Domain.Appraisals;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Appraisal.Tests.Domain;

/// <summary>The building-detail fields printed in the appraisal book survive Update and a re-appraisal copy.</summary>
public class BuildingDetailBookFieldsTests
{
    private static BuildingAppraisalDetail NewBuilding()
        => AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now)
            .AddBuildingProperty().BuildingDetail!;

    [Fact]
    public void Update_and_CopyFrom_keep_the_book_fields()
    {
        var building = NewBuilding();

        building.Update(
            buildingWidth: 8.5m, buildingLength: 12m, spaceDivision: "2 bedrooms",
            maintenanceType: "99", maintenanceTypeOther: "Annual repaint",
            doorType: ["01", "99"], doorTypeOther: "Glass sliding",
            windowType: ["03"], windowTypeOther: null,
            sanitaryType: ["01", "99"], sanitaryTypeOther: "Bidet",
            buildingSystemType: ["08"], buildingSystemTypeOther: null,
            constructionLicenseNumber: "ก-123/2566");

        var copy = BuildingAppraisalDetail.CopyFrom(building, Guid.NewGuid());

        foreach (var b in new[] { building, copy })
        {
            Assert.Equal(8.5m, b.BuildingWidth);
            Assert.Equal(12m, b.BuildingLength);
            Assert.Equal("2 bedrooms", b.SpaceDivision);
            Assert.Equal("99", b.MaintenanceType);
            Assert.Equal("Annual repaint", b.MaintenanceTypeOther);
            Assert.Equal(["01", "99"], b.DoorType);
            Assert.Equal("Glass sliding", b.DoorTypeOther);
            Assert.Equal(["03"], b.WindowType);
            Assert.Equal(["01", "99"], b.SanitaryType);
            Assert.Equal("Bidet", b.SanitaryTypeOther);
            Assert.Equal(["08"], b.BuildingSystemType);
            Assert.Equal("ก-123/2566", b.ConstructionLicenseNumber);
        }

        Assert.NotSame(building.DoorType, copy.DoorType);
        Assert.NotSame(building.SanitaryType, copy.SanitaryType);
    }

    [Fact]
    public void NotAppraisableReason_survives_Update_and_CopyFrom()
    {
        var building = NewBuilding();

        building.Update(isAppraisable: false, notAppraisableReason: "Temporary structure");

        var copy = BuildingAppraisalDetail.CopyFrom(building, Guid.NewGuid());

        Assert.False(building.IsAppraisable);
        Assert.Equal("Temporary structure", building.NotAppraisableReason);
        Assert.Equal("Temporary structure", copy.NotAppraisableReason);
    }

    [Fact]
    public void Land_flood_and_pollution_survive_Update_and_CopyFrom()
    {
        var land = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now)
            .AddLandProperty().LandDetail!;

        land.Update(hasFloodProblem: true, floodProblemRemark: "Wet season",
            hasPollutionProblem: true, pollutionType: ["02", "99"], pollutionTypeOther: "Smell",
            developmentSuitabilityType: "02", urbanPlanningColor: "99", urbanPlanningColorOther: "Pink");

        var copy = LandAppraisalDetail.CopyFrom(land, Guid.NewGuid());

        foreach (var l in new[] { land, copy })
        {
            Assert.True(l.HasFloodProblem);
            Assert.Equal("Wet season", l.FloodProblemRemark);
            Assert.True(l.HasPollutionProblem);
            Assert.Equal(["02", "99"], l.PollutionType);
            Assert.Equal("Smell", l.PollutionTypeOther);
            Assert.Equal("02", l.DevelopmentSuitabilityType);
            Assert.Equal("99", l.UrbanPlanningColor);
            Assert.Equal("Pink", l.UrbanPlanningColorOther);
        }

        Assert.NotSame(land.PollutionType, copy.PollutionType);
    }

    [Fact]
    public void Condo_flood_and_pollution_survive_Update_and_CopyFrom()
    {
        var condo = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now)
            .AddCondoProperty().CondoDetail!;

        condo.Update(hasFloodProblem: false, hasPollutionProblem: true, pollutionType: ["01"], urbanPlanningColor: "03");

        var copy = CondoAppraisalDetail.CopyFrom(condo, Guid.NewGuid());

        foreach (var c in new[] { condo, copy })
        {
            Assert.False(c.HasFloodProblem);
            Assert.Null(c.FloodProblemRemark);
            Assert.True(c.HasPollutionProblem);
            Assert.Equal(["01"], c.PollutionType);
            Assert.Equal("03", c.UrbanPlanningColor);
        }
    }
}
