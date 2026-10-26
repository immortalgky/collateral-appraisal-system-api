using Appraisal.Application.Features.Project.GetProjectUnits;
using Appraisal.Domain.Projects;
using Appraisal.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Appraisal.Tests.Application.Features;

public class GetProjectUnitsQueryHandlerTests
{
    private readonly AppraisalDbContext _db = new(
        new DbContextOptionsBuilder<AppraisalDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task Units_carry_their_tower_and_model_ids()
    {
        var appraisalId = Guid.NewGuid();
        var project = Project.Create(appraisalId, ProjectType.Condo);
        _db.Projects.Add(project);
        var towerId = Guid.NewGuid();
        var modelId = Guid.NewGuid();
        var linked = ProjectUnit.CreateCondo(project.Id, Guid.NewGuid(), 1, towerName: "A", modelType: "M1");
        var unlinked = ProjectUnit.CreateCondo(project.Id, Guid.NewGuid(), 2);
        _db.ProjectUnits.AddRange(linked, unlinked);
        _db.Entry(linked).Property(nameof(ProjectUnit.ProjectTowerId)).CurrentValue = towerId;
        _db.Entry(linked).Property(nameof(ProjectUnit.ProjectModelId)).CurrentValue = modelId;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await new GetProjectUnitsQueryHandler(_db)
            .Handle(new GetProjectUnitsQuery(appraisalId), CancellationToken.None);

        Assert.Equal(towerId, result.Units[0].ProjectTowerId);
        Assert.Equal(modelId, result.Units[0].ProjectModelId);
        Assert.Null(result.Units[1].ProjectTowerId);
        Assert.Null(result.Units[1].ProjectModelId);
    }
}
