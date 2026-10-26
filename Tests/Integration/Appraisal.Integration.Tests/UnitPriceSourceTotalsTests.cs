using Appraisal.Application.Features.Project.ImportProjectUnitPrices;
using Appraisal.Domain.Projects;
using Appraisal.Infrastructure;
using Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>The unit-price source totals aggregate (GroupBy + nullable Sum) on real SQL Server.</summary>
[Collection("Integration")]
public class UnitPriceSourceTotalsTests(IntegrationTestFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Totals_sum_every_unit_sold_or_not_and_skip_unpriced_ones()
    {
        Guid appraisalId;
        using (var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
            appraisal.SetAppraisalNumber($"UPS-{Guid.NewGuid():N}"[..18]);
            db.Appraisals.Add(appraisal);
            await db.SaveChangesAsync(Ct);
            appraisalId = appraisal.Id;

            var project = Project.Create(appraisal.Id, ProjectType.Condo, projectName: "Totals Test");
            var units = new List<ProjectUnit>
            {
                ProjectUnit.CreateCondo(project.Id, Guid.Empty, 1, 1, "A", "9/1", "101", null, 30m, 1m),
                ProjectUnit.CreateCondo(project.Id, Guid.Empty, 2, 1, "A", "9/2", "102", null, 30m, 1m),
                ProjectUnit.CreateCondo(project.Id, Guid.Empty, 3, 1, "A", "9/3", "103", null, 30m, 1m),
            };
            units[1].MarkSoldByReappraisal();
            project.ImportUnits("units.xlsx", null, units);
            db.Projects.Add(project);

            var prices = new (ProjectUnit Unit, decimal? Value, decimal? Force)[]
                { (units[0], 1_000m, 700m), (units[1], 2_000m, 1_400m), (units[2], null, null) };
            foreach (var (unit, value, force) in prices)
            {
                var price = ProjectUnitPrice.Create(unit.Id);
                price.ApplyImportedValues(null, value, force, null);
                db.ProjectUnitPrices.Add(price);
            }
            await db.SaveChangesAsync(Ct);
        }

        using (var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var result = await new GetUnitPriceSourceQueryHandler(db)
                .Handle(new GetUnitPriceSourceQuery(appraisalId), Ct);

            Assert.Equal(2, result.PricedUnitCount);
            Assert.Equal(3_000m, result.TotalAppraisalValueRounded);
            Assert.Equal(2_100m, result.TotalForceSellingPrice);
        }
    }
}
