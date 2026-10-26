using Appraisal.Application.Features.DecisionSummary.UpdateForceSaleRate;
using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Projects;
using Appraisal.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared.Configuration;
using Shared.Data;
using Shared.Time;

namespace Appraisal.Tests.Application.Features;

/// <summary>
/// A rate-only edit rewrites ForcedSaleValue as appraised x rate for a normal appraisal, but must leave a
/// block's forced sale (the sum of its units' ForceSellingPrice) alone.
/// </summary>
public class UpdateForceSaleRateBlockTests
{
    private static async Task<(AppraisalDbContext Db, Guid AppraisalId)> SeedAsync(bool block)
    {
        var options = new DbContextOptionsBuilder<AppraisalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppraisalDbContext(options);

        var appraisalId = Guid.NewGuid();
        if (block)
        {
            db.Projects.Add(Project.Create(appraisalId, ProjectType.Condo));
            await db.SaveChangesAsync();
        }

        // Left unsaved (the audit columns are stamped by an interceptor this context does not have); the
        // handler finds it in the change tracker.
        var valuation = ValuationAnalysis.Create(appraisalId, "Combined", new DateTime(2026, 10, 1));
        valuation.UpdateSummary("Combined", new DateTime(2026, 10, 1), 1_000_000m, 700_000m, 50_000m);
        db.ValuationAnalyses.Add(valuation);
        return (db, appraisalId);
    }

    private static UpdateForceSaleRateCommandHandler Handler(AppraisalDbContext db) =>
        new(db,
            new ForceSaleRateResolver(
                Substitute.For<ISqlConnectionFactory>(),
                Substitute.For<ISystemConfigurationReader>(),
                Substitute.For<ILogger<ForceSaleRateResolver>>()),
            new AppraisalDateResolver(db),
            Substitute.For<IDateTimeProvider>());

    [Fact]
    public async Task Block_KeepsTheOverride_ButWritesNoForcedSaleValue()
    {
        var (db, appraisalId) = await SeedAsync(block: true);
        await using var _ = db;

        await Handler(db).Handle(new UpdateForceSaleRateCommand(appraisalId, 50m), CancellationToken.None);

        var row = db.ValuationAnalyses.Local.Single();
        Assert.Equal(50m, row.ForceSaleRate);
        Assert.Equal(700_000m, row.ForcedSaleValue);
        Assert.Equal(1_000_000m, row.AppraisedValue);
    }

    [Fact]
    public async Task NormalAppraisal_StillRecomputesForcedSaleFromTheRate()
    {
        var (db, appraisalId) = await SeedAsync(block: false);
        await using var _ = db;

        await Handler(db).Handle(new UpdateForceSaleRateCommand(appraisalId, 50m), CancellationToken.None);

        Assert.Equal(500_000m, db.ValuationAnalyses.Local.Single().ForcedSaleValue);
    }
}
