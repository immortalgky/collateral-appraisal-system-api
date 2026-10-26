using Appraisal.Application.Features.Project.ImportProjectUnitPrices;
using Appraisal.Domain.Projects;
using Appraisal.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Appraisal.Tests.Application.Features.ImportProjectUnitPrices;

public class GetUnitPriceSourceQueryHandlerTests
{
    private readonly AppraisalDbContext _db = new(
        new DbContextOptionsBuilder<AppraisalDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private async Task<Guid> SeedProject(Guid appraisalId, params (bool sold, decimal? value, decimal? force)[] units)
    {
        var project = Project.Create(appraisalId, ProjectType.Condo);
        _db.Projects.Add(project);
        foreach (var (sold, value, force) in units)
        {
            var unit = ProjectUnit.CreateCondo(project.Id, Guid.NewGuid(), 1);
            if (sold) unit.MarkSoldByReappraisal();
            _db.ProjectUnits.Add(unit);
            var price = ProjectUnitPrice.Create(unit.Id);
            price.ApplyImportedValues(null, value, force, null);
            _db.ProjectUnitPrices.Add(price);
        }
        await _db.SaveChangesAsync();
        return project.Id;
    }

    [Fact]
    public async Task Totals_sum_every_unit_of_the_project_sold_or_not_and_ignore_other_projects()
    {
        var appraisalId = Guid.NewGuid();
        await SeedProject(appraisalId, (false, 1_000m, 700m), (true, 2_000m, 1_400m), (false, null, null), (false, null, 500m));
        await SeedProject(Guid.NewGuid(), (false, 50_000m, 35_000m));

        var result = await new GetUnitPriceSourceQueryHandler(_db)
            .Handle(new GetUnitPriceSourceQuery(appraisalId), CancellationToken.None);

        Assert.Equal(2, result.PricedUnitCount);
        Assert.Equal(3_000m, result.TotalAppraisalValueRounded);
        Assert.Equal(2_100m, result.TotalForceSellingPrice);
    }

    [Fact]
    public async Task No_project_returns_null_totals()
    {
        var result = await new GetUnitPriceSourceQueryHandler(_db)
            .Handle(new GetUnitPriceSourceQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(0, result.PricedUnitCount);
        Assert.Null(result.TotalAppraisalValueRounded);
        Assert.Null(result.TotalForceSellingPrice);
    }
}
