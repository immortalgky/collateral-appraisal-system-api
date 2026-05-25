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
    public void Land_AddModel_WithNullTowerId_Succeeds()
    {
        // Land projects follow LB rules: no tower required
        var project = CreateLandProject();
        var model = project.AddModel(projectTowerId: null, modelName: "Plot-A");
        Assert.Null(model.ProjectTowerId);
        Assert.Single(project.Models);
    }

    [Fact]
    public void Land_AddModel_WithNonNullTowerId_ThrowsInvalidProjectStateException()
    {
        // Land projects follow LB rules: tower must be null
        var project = CreateLandProject();
        var fakeTowerId = Guid.NewGuid();

        var ex = Assert.Throws<InvalidProjectStateException>(
            () => project.AddModel(projectTowerId: fakeTowerId, modelName: "Plot-A"));

        Assert.Contains("must not be linked to a tower", ex.Message, StringComparison.OrdinalIgnoreCase);
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
