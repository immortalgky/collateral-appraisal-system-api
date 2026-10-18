using Appraisal.Domain.Projects;

namespace Appraisal.Tests.Domain;

/// <summary>
/// Covers Project.ImportUnits on a Condo project: units must link to a tower and model, and a unit
/// that has a ModelType but no TowerName must stay unlinked instead of throwing.
/// </summary>
public class ProjectCondoImportUnitsTests
{
    [Fact]
    public void Condo_ImportUnits_LinksUnitsToTowerAndModel()
    {
        var project = Project.Create(Guid.NewGuid(), ProjectType.Condo);
        var unit = ProjectUnit.CreateCondo(
            project.Id, Guid.Empty, 1, floor: 3, towerName: "A", roomNumber: "301", modelType: "TypeA");

        project.ImportUnits("units.csv", documentId: null, [unit]);

        var tower = Assert.Single(project.Towers);
        var model = Assert.Single(project.Models);
        Assert.Equal(tower.Id, unit.ProjectTowerId);
        Assert.Equal(model.Id, unit.ProjectModelId);
        Assert.Equal(tower.Id, model.ProjectTowerId);
    }

    [Fact]
    public void Condo_ImportUnits_ModelTypeWithoutTowerName_DoesNotThrowAndStaysUnlinked()
    {
        var project = Project.Create(Guid.NewGuid(), ProjectType.Condo);
        var unit = ProjectUnit.CreateCondo(
            project.Id, Guid.Empty, 1, roomNumber: "301", modelType: "TypeA");

        var ex = Record.Exception(() => project.ImportUnits("units.csv", documentId: null, [unit]));

        Assert.Null(ex);
        Assert.Null(unit.ProjectTowerId);
        Assert.Null(unit.ProjectModelId);
    }
}
