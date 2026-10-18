using Appraisal.Application.Services;
using Appraisal.Domain.Projects;
using Appraisal.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared.Configuration;
using Shared.Data;
using Shared.Data.Outbox;
using Shared.Time;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Appraisal.Tests.Application.Services;

/// <summary>
/// The block branch of <see cref="AppraisalValuationSummaryService.RecomputeAsync"/>: every unit counts
/// (sold or not, with or without a model) and the forced-sale value is the plain sum of the units'
/// ForceSellingPrice. GetDecisionSummary's block SQL must give the same totals (KEEP IN SYNC).
/// </summary>
public class BlockValuationRollupTests
{
    [Fact]
    public async Task EveryUnitCounts_AndForcedSaleIsTheSumOfUnitForceSellingPrices()
    {
        var dbOptions = new DbContextOptionsBuilder<AppraisalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppraisalDbContext(dbOptions);

        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "Initial", "Normal", DateTime.Now);
        var project = Project.Create(appraisal.Id, ProjectType.Condo);
        db.Projects.Add(project);

        // Unit 3 is sold; none has a model (ProjectModelId NULL) -- the old INNER JOIN on ProjectModels
        // dropped such units, and the old filter dropped the sold one.
        AddPricedUnit(db, project, 1, appraisal: 1_000_000.10m, force: 700_000.55m, coverage: 100_500m);
        AddPricedUnit(db, project, 2, appraisal: 2_000_000m, force: 1_400_000m, coverage: 200_250m);
        AddPricedUnit(db, project, 3, appraisal: 3_000_000m, force: 2_100_000m, coverage: 300_000m, sold: true);
        await db.SaveChangesAsync();

        var outbox = Substitute.For<IIntegrationEventOutbox>();
        var service = new AppraisalValuationSummaryService(
            db,
            outbox,
            Substitute.For<IDateTimeProvider>(),
            new ForceSaleRateResolver(
                Substitute.For<ISqlConnectionFactory>(),
                Substitute.For<ISystemConfigurationReader>(),
                Substitute.For<ILogger<ForceSaleRateResolver>>()),
            Substitute.For<ILogger<AppraisalValuationSummaryService>>());

        await service.RecomputeAsync(appraisal.Id, CancellationToken.None, appraisal: appraisal);

        var row = db.ValuationAnalyses.Local.Single();
        Assert.Equal(6_000_000.10m, row.AppraisedValue);
        // Not total x rate, and not rounded to 1,000: an imported price list is kept as stored.
        Assert.Equal(4_200_000.55m, row.ForcedSaleValue);
        Assert.Equal(600_750m, row.InsuranceValue);
    }

    private static void AddPricedUnit(
        AppraisalDbContext db, Project project, int seq, decimal appraisal, decimal force, decimal coverage,
        bool sold = false)
    {
        var unit = ProjectUnit.CreateCondo(project.Id, Guid.Empty, seq, roomNumber: $"10{seq}");
        if (sold) unit.SetSaleInfo(true, UnitPurchaseMethod.Cash, null);
        db.ProjectUnits.Add(unit);

        var price = ProjectUnitPrice.Create(unit.Id);
        price.ApplyImportedValues(null, appraisal, force, coverage);
        db.ProjectUnitPrices.Add(price);
    }
}
