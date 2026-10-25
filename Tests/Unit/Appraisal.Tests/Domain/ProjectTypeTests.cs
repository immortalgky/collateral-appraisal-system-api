using Appraisal.Domain.Projects;
using Appraisal.Domain.Projects.Exceptions;

namespace Appraisal.Tests.Domain;

/// <summary>
/// Tests for <see cref="ProjectType"/> value object and Land-type aggregate invariants.
/// </summary>
public class ProjectTypeTests
{
    // ---------------------------------------------------------------------------
    // ToCode / FromCode round-trip
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("U")]
    [InlineData("LB")]
    [InlineData("L")]
    public void ToCode_ReturnsExpectedCode(string code)
    {
        Assert.Equal(code, ProjectType.FromCode(code).ToCode());
    }

    [Theory]
    [InlineData("U")]
    [InlineData("LB")]
    [InlineData("L")]
    public void FromCode_FromString_AreEquivalent(string code)
    {
        Assert.Equal(ProjectType.FromString(code), ProjectType.FromCode(code));
    }

    [Theory]
    [InlineData("U")]
    [InlineData("LB")]
    [InlineData("L")]
    public void ToCode_FromCode_RoundTrip(string code)
    {
        var original = ProjectType.FromCode(code);
        var roundTripped = ProjectType.FromCode(original.ToCode());
        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void FromCode_UnknownCode_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ProjectType.FromCode("XX"));
    }

    [Theory]
    [InlineData("U", true)]
    [InlineData("LB", true)]
    [InlineData("L", true)]
    [InlineData("XX", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidCode_ReturnsExpected(string? code, bool expected)
    {
        Assert.Equal(expected, ProjectType.IsValidCode(code));
    }

    // ---------------------------------------------------------------------------
    // IsLandAndBuildingLike
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("LB", true)]
    [InlineData("L", true)]
    [InlineData("U", false)]
    public void IsLandAndBuildingLike_ReturnsExpected(string code, bool expected)
    {
        Assert.Equal(expected, ProjectType.FromCode(code).IsLandAndBuildingLike());
    }

    [Theory]
    [InlineData("LB", true)]
    [InlineData("L", true)]
    [InlineData("U", false)]
    [InlineData("XX", false)]
    [InlineData(null, false)]
    public void IsLandAndBuildingLikeCode_ReturnsExpected(string? code, bool expected)
    {
        Assert.Equal(expected, ProjectType.IsLandAndBuildingLikeCode(code));
    }

    // ---------------------------------------------------------------------------
    // Land type follows LandAndBuilding factory / invariant paths
    // ---------------------------------------------------------------------------

    private static Project CreateLandProject()
    {
        var appraisalId = Guid.NewGuid();
        return Project.Create(appraisalId, ProjectType.Land);
    }

    [Fact]
    public void Land_AddModel_Throws()
    {
        // A bare plot has no building, so no model -- whatever the tower argument is.
        var project = CreateLandProject();

        var ex = Assert.Throws<InvalidProjectStateException>(
            () => project.AddModel(projectTowerId: null, modelName: "Plot-A"));

        Assert.Contains("AddModel", ex.Message);
        Assert.Empty(project.Models);
    }

    [Fact]
    public void Land_AddModel_WithTowerId_Throws()
    {
        var project = CreateLandProject();

        Assert.Throws<InvalidProjectStateException>(
            () => project.AddModel(projectTowerId: Guid.NewGuid(), modelName: "Plot-A"));
    }

    [Fact]
    public void Land_UpdateModel_Throws()
    {
        var project = CreateLandProject();

        Assert.Throws<InvalidProjectStateException>(
            () => project.UpdateModel(Guid.NewGuid(), projectTowerId: null, modelName: "Plot-A"));
    }

    [Fact]
    public void LandAndBuilding_AddModel_StillSucceeds()
    {
        var project = Project.Create(Guid.NewGuid(), ProjectType.LandAndBuilding);

        var model = project.AddModel(projectTowerId: null, modelName: "House-A");

        Assert.Single(project.Models);
        Assert.Equal("House-A", model.ModelName);
    }

    [Fact]
    public void Land_ImportUnits_CreatesNoModels()
    {
        var project = CreateLandProject();
        var unit = ProjectUnit.CreateLandAndBuilding(project.Id, Guid.Empty, 1, "P1", null, null, null, 50m, null, 1_000_000m);

        project.ImportUnits("units.xlsx", null, [unit]);

        Assert.Empty(project.Models);
        Assert.Null(unit.ProjectModelId);
    }

    [Fact]
    public void Land_AddTower_ThrowsInvalidProjectStateException()
    {
        // AddTower is Condo-only; Land should throw
        var project = CreateLandProject();

        var ex = Assert.Throws<InvalidProjectStateException>(() => project.AddTower());
        Assert.Contains("Condo", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Land_GetOrCreateLand_Succeeds()
    {
        // GetOrCreateLand uses RequireLandAndBuildingLike — should work for Land
        var project = CreateLandProject();
        var land = project.GetOrCreateLand();
        Assert.NotNull(land);
    }

    [Fact]
    public void Land_Create_WithBuiltOnTitleDeedNumber_ThrowsArgumentException()
    {
        // BuiltOnTitleDeedNumber is Condo-only; Land follows LB rules (no title deed)
        Assert.Throws<ArgumentException>(() =>
            Project.Create(Guid.NewGuid(), ProjectType.Land, builtOnTitleDeedNumber: "DEED-123"));
    }

    [Fact]
    public void Land_Create_WithLicenseExpirationDate_Succeeds()
    {
        // LicenseExpirationDate is valid for LandAndBuildingLike (includes Land)
        var expiry = DateTime.UtcNow.AddYears(1);
        var project = Project.Create(Guid.NewGuid(), ProjectType.Land, licenseExpirationDate: expiry);
        Assert.Equal(expiry, project.LicenseExpirationDate);
    }
}
