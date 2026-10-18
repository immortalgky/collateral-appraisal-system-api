using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Events;
using Appraisal.Infrastructure;
using Integration.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// RecomputeAsync sums insurance over the properties the caller loaded through the repository. It does
/// not reload them (the owned graph is about 21 tables) and it must not sum an unloaded collection (that
/// would silently write InsuranceValue = 0), so an unloaded collection throws.
/// </summary>
[Collection("Integration")]
public class RecomputeLoadedPropertiesContractTests(IntegrationTestFixture fixture)
{
    private IServiceScope CreateScope()
        => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    // One building with a single insurable row (700,000) in a group, so the pricing event can find it.
    private async Task<(Guid AppraisalId, Guid GroupId)> SeedBuildingInGroupAsync(CancellationToken ct)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
        appraisal.SetAppraisalNumber($"RLP-{Guid.NewGuid():N}"[..18]);
        var building = appraisal.AddBuildingProperty();
        building.BuildingDetail!.Update(ownerName: "Owner");
        building.BuildingDetail.AddDepreciationDetail("Gross", isBuilding: true, priceAfterDepreciation: 700_000m);
        var group = appraisal.CreateGroup("G1");
        db.Appraisals.Add(appraisal);
        await db.SaveChangesAsync(ct);
        appraisal.AddPropertyToGroup(group.Id, building.Id);
        await db.SaveChangesAsync(ct);
        return (appraisal.Id, group.Id);
    }

    private static async Task<decimal?> InsuranceAsync(AppraisalDbContext db, Guid appraisalId, CancellationToken ct)
        => (await db.ValuationAnalyses.AsNoTracking().FirstAsync(v => v.AppraisalId == appraisalId, ct)).InsuranceValue;

    [Fact]
    public async Task RecomputeAsync_throws_when_the_properties_of_a_normal_appraisal_are_not_loaded()
    {
        var ct = TestContext.Current.CancellationToken;
        var (appraisalId, _) = await SeedBuildingInGroupAsync(ct);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        await db.Appraisals.FirstAsync(a => a.Id == appraisalId, ct); // tracked, Properties not loaded

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<AppraisalValuationSummaryService>()
                .RecomputeAsync(appraisalId, ct));
        Assert.Contains("GetByIdWithPropertiesAsync", ex.Message);
    }

    [Fact]
    public async Task A_just_added_and_saved_aggregate_needs_the_repository_load_before_RecomputeAsync()
    {
        // AppraisalCreationService recomputes on the aggregate it added and saved itself. EF does not mark
        // the Properties of such an aggregate as loaded, so it loads them through the repository first.
        var ct = TestContext.Current.CancellationToken;
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
        appraisal.SetAppraisalNumber($"RLP-{Guid.NewGuid():N}"[..18]);
        var building = appraisal.AddBuildingProperty();
        building.BuildingDetail!.Update(ownerName: "Owner");
        building.BuildingDetail.AddDepreciationDetail("Gross", isBuilding: true, priceAfterDepreciation: 700_000m);
        db.Appraisals.Add(appraisal);
        await db.SaveChangesAsync(ct);
        Assert.False(db.Entry(appraisal).Collection(a => a.Properties).IsLoaded);

        await scope.ServiceProvider.GetRequiredService<IAppraisalRepository>()
            .GetByIdWithPropertiesAsync(appraisal.Id, ct);
        Assert.True(db.Entry(appraisal).Collection(a => a.Properties).IsLoaded);
        await scope.ServiceProvider.GetRequiredService<AppraisalValuationSummaryService>()
            .RecomputeAsync(appraisal.Id, ct);
        await db.SaveChangesAsync(ct);

        Assert.Equal(700_000m, await InsuranceAsync(db, appraisal.Id, ct));
    }

    [Fact]
    public async Task Pricing_save_event_computes_insurance_when_the_appraisal_is_not_tracked_yet()
    {
        var ct = TestContext.Current.CancellationToken;
        var (appraisalId, groupId) = await SeedBuildingInGroupAsync(ct);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        await scope.ServiceProvider.GetRequiredService<IPublisher>()
            .Publish(new AppraisalFinalValuesChangedEvent(groupId), ct);
        await db.SaveChangesAsync(ct);

        Assert.Equal(700_000m, await InsuranceAsync(db, appraisalId, ct));
    }

    [Fact]
    public async Task Pricing_save_event_loads_the_properties_of_an_appraisal_already_tracked_without_them()
    {
        var ct = TestContext.Current.CancellationToken;
        var (appraisalId, groupId) = await SeedBuildingInGroupAsync(ct);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var tracked = await db.Appraisals.FirstAsync(a => a.Id == appraisalId, ct);
        Assert.False(db.Entry(tracked).Collection(a => a.Properties).IsLoaded);

        await scope.ServiceProvider.GetRequiredService<IPublisher>()
            .Publish(new AppraisalFinalValuesChangedEvent(groupId), ct);
        await db.SaveChangesAsync(ct);

        Assert.True(db.Entry(tracked).Collection(a => a.Properties).IsLoaded);
        Assert.Equal(700_000m, await InsuranceAsync(db, appraisalId, ct));
    }

    // Any read of AppraisalProperties is the repository's full property load. ([LandTitles] is not used:
    // EF leaves that table out of the split query when no loaded property has a land detail, as here.)
    // The capture must exist BEFORE the DbContext is resolved — a context created earlier reports nothing —
    // so callers note how many commands the setup issued and count only the ones after it.
    private static int PropertyReads(SqlCapture sql, int skip)
        => sql.Commands.Skip(skip).Count(c => c.Contains("[AppraisalProperties]"));

    [Fact]
    public async Task Pricing_save_event_reuses_the_loaded_properties_without_querying_them_again()
    {
        var ct = TestContext.Current.CancellationToken;
        var (appraisalId, groupId) = await SeedBuildingInGroupAsync(ct);

        using var sql = new SqlCapture();
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        await scope.ServiceProvider.GetRequiredService<IAppraisalRepository>()
            .GetByIdWithPropertiesAsync(appraisalId, ct);
        var setup = sql.Commands.Count;
        Assert.True(setup > 0); // the capture sees this context's queries

        // Two events in one save (two changed analyses): neither may reload the graph.
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        await publisher.Publish(new AppraisalFinalValuesChangedEvent(groupId), ct);
        await publisher.Publish(new AppraisalFinalValuesChangedEvent(groupId), ct);
        await db.SaveChangesAsync(ct);

        Assert.Equal(0, PropertyReads(sql, setup));
        Assert.Equal(700_000m, await InsuranceAsync(db, appraisalId, ct));
    }

    [Fact]
    public async Task Pricing_save_event_control_reads_the_property_graph_when_the_tracked_appraisal_lacks_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var (appraisalId, groupId) = await SeedBuildingInGroupAsync(ct);

        using var sql = new SqlCapture();
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        await db.Appraisals.FirstAsync(a => a.Id == appraisalId, ct);
        var setup = sql.Commands.Count;

        await scope.ServiceProvider.GetRequiredService<IPublisher>()
            .Publish(new AppraisalFinalValuesChangedEvent(groupId), ct);

        Assert.True(PropertyReads(sql, setup) > 0);
    }

    [Fact]
    public async Task Pricing_save_event_recomputes_for_an_appraisal_added_in_the_context_but_not_saved()
    {
        // The real flow cannot raise this event on an Added appraisal (analyses of a new appraisal are
        // Added too, and Added analyses never emit it), so the event is published directly while the
        // aggregate is Added. The repository query would find no row; the handler must use the Local
        // instance, whose properties were all added by this context.
        var ct = TestContext.Current.CancellationToken;
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
        appraisal.SetAppraisalNumber($"RLP-{Guid.NewGuid():N}"[..18]);
        var building = appraisal.AddBuildingProperty();
        building.BuildingDetail!.Update(ownerName: "Owner");
        building.BuildingDetail.AddDepreciationDetail("Gross", isBuilding: true, priceAfterDepreciation: 700_000m);
        var group = appraisal.CreateGroup("G1");
        db.Appraisals.Add(appraisal);
        Assert.Equal(EntityState.Added, db.Entry(appraisal).State);

        await scope.ServiceProvider.GetRequiredService<IPublisher>()
            .Publish(new AppraisalFinalValuesChangedEvent(group.Id), ct);
        await db.SaveChangesAsync(ct);

        Assert.Equal(700_000m, await InsuranceAsync(db, appraisal.Id, ct));
    }
}
