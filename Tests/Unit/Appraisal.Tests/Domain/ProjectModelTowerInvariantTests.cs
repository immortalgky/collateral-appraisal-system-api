using Appraisal.Domain.Projects;
using Appraisal.Domain.Projects.Exceptions;

namespace Appraisal.Tests.Domain;

/// <summary>
/// Verifies the tower-linkage and uniqueness invariants on Project.AddModel and Project.UpdateModel
/// introduced in Task #2 of the Block pricing-context feature.
/// </summary>
public class ProjectModelTowerInvariantTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static Project CreateCondoProject()
    {
        var appraisalId = Guid.NewGuid();
        return Project.Create(appraisalId, ProjectType.Condo);
    }

    private static Project CreateVillageProject()
    {
        var appraisalId = Guid.NewGuid();
        return Project.Create(appraisalId, ProjectType.LandAndBuilding);
    }

    // ---------------------------------------------------------------------------
    // Condo — AddModel invariants
    // ---------------------------------------------------------------------------

    [Fact]
    public void Condo_AddModel_WithNullTowerId_ThrowsInvalidProjectStateException()
    {
        var project = CreateCondoProject();
        project.AddTower(); // add a tower so the project is valid

        var ex = Assert.Throws<InvalidProjectStateException>(
            () => project.AddModel(projectTowerId: null, modelName: "TypeA"));

        Assert.Contains("required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Condo_AddModel_WithNonExistentTowerId_ThrowsInvalidProjectStateException()
    {
        var project = CreateCondoProject();
        var nonExistentTowerId = Guid.NewGuid();

        var ex = Assert.Throws<InvalidProjectStateException>(
            () => project.AddModel(projectTowerId: nonExistentTowerId, modelName: "TypeA"));

        Assert.Contains("does not exist", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Condo_AddModel_DuplicateTowerAndName_ThrowsInvalidProjectStateException()
    {
        var project = CreateCondoProject();
        var tower = project.AddTower();

        // First add — should succeed
        project.AddModel(projectTowerId: tower.Id, modelName: "TypeA");

        // Second add with same tower + name — should fail
        var ex = Assert.Throws<InvalidProjectStateException>(
            () => project.AddModel(projectTowerId: tower.Id, modelName: "TypeA"));

        Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Condo_AddModel_SameNameInDifferentTowers_Succeeds()
    {
        var project = CreateCondoProject();
        var towerA = project.AddTower();
        var towerB = project.AddTower();

        // Same model name on different towers should not conflict
        var modelInA = project.AddModel(projectTowerId: towerA.Id, modelName: "TypeA");
        var modelInB = project.AddModel(projectTowerId: towerB.Id, modelName: "TypeA");

        Assert.Equal(towerA.Id, modelInA.ProjectTowerId);
        Assert.Equal(towerB.Id, modelInB.ProjectTowerId);
        Assert.Equal(2, project.Models.Count);
    }

    [Fact]
    public void Condo_AddModel_DifferentNamesInSameTower_Succeeds()
    {
        var project = CreateCondoProject();
        var tower = project.AddTower();

        var model1 = project.AddModel(projectTowerId: tower.Id, modelName: "TypeA");
        var model2 = project.AddModel(projectTowerId: tower.Id, modelName: "TypeB");

        Assert.Equal(tower.Id, model1.ProjectTowerId);
        Assert.Equal(tower.Id, model2.ProjectTowerId);
        Assert.Equal(2, project.Models.Count);
    }

    // ---------------------------------------------------------------------------
    // Village (LandAndBuilding) — AddModel invariants
    // ---------------------------------------------------------------------------

    [Fact]
    public void Village_AddModel_WithNonNullTowerId_ThrowsInvalidProjectStateException()
    {
        var project = CreateVillageProject();
        var fakeTowerId = Guid.NewGuid();

        var ex = Assert.Throws<InvalidProjectStateException>(
            () => project.AddModel(projectTowerId: fakeTowerId, modelName: "House55"));

        Assert.Contains("must not be linked to a tower", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Village_AddModel_DuplicateName_ThrowsInvalidProjectStateException()
    {
        var project = CreateVillageProject();

        project.AddModel(projectTowerId: null, modelName: "House55");

        var ex = Assert.Throws<InvalidProjectStateException>(
            () => project.AddModel(projectTowerId: null, modelName: "House55"));

        Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Village_AddModel_UniqueNames_Succeeds()
    {
        var project = CreateVillageProject();

        var model1 = project.AddModel(projectTowerId: null, modelName: "House55");
        var model2 = project.AddModel(projectTowerId: null, modelName: "House60");

        Assert.Null(model1.ProjectTowerId);
        Assert.Null(model2.ProjectTowerId);
        Assert.Equal(2, project.Models.Count);
    }

    // ---------------------------------------------------------------------------
    // UpdateModel invariants
    // ---------------------------------------------------------------------------

    [Fact]
    public void Condo_UpdateModel_MoveToDifferentTower_Succeeds()
    {
        var project = CreateCondoProject();
        var towerA = project.AddTower();
        var towerB = project.AddTower();
        var model = project.AddModel(projectTowerId: towerA.Id, modelName: "TypeA");

        // Move model to tower B (name not in tower B, so no conflict)
        var updated = project.UpdateModel(model.Id, projectTowerId: towerB.Id, modelName: "TypeA");

        Assert.Equal(towerB.Id, updated.ProjectTowerId);
    }

    [Fact]
    public void Village_UpdateModel_WithNonNullTowerId_ThrowsInvalidProjectStateException()
    {
        var project = CreateVillageProject();
        var model = project.AddModel(projectTowerId: null, modelName: "House55");

        var ex = Assert.Throws<InvalidProjectStateException>(
            () => project.UpdateModel(model.Id, projectTowerId: Guid.NewGuid(), modelName: "House55"));

        Assert.Contains("must not be linked to a tower", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
