using Appraisal.Application.Features.Appraisals.CreateLandProperty;
using Appraisal.Application.Features.Appraisals.CreatePropertyGroup;
using Appraisal.Application.Features.Appraisals.UpdateCondoProperty;
using Appraisal.Application.Features.Appraisals.UpdateLandAndBuildingProperty;
using Appraisal.Contracts.Appraisals.Dto;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Integration.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// Property saves go through the real MediatR pipeline, so TransactionalBehavior's SaveChanges is what
/// persists them. These pin two things:
///   • the handler does not call DbSet.Update on the tracked aggregate. Change tracking already saves
///     the edits, and Update() only adds a full-column UPDATE plus an audit stamp on rows nobody touched;
///   • RecomputeAsync reuses the properties the handler already loaded instead of reloading the whole
///     owned graph (about 21 tables) from the database.
/// </summary>
[Collection("Integration")]
public class PropertySaveWriteScopeTests(IntegrationTestFixture fixture)
{
    private IServiceScope CreateScope()
        => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    private static decimal RoundTo1000(decimal v) => Math.Round(v / 1000, MidpointRounding.AwayFromZero) * 1000;

    private async Task<(Guid AppraisalId, Guid EditedId, Guid OtherId)> SeedTwoBuildingsAsync(CancellationToken ct)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
        appraisal.SetAppraisalNumber($"PSW-{Guid.NewGuid():N}"[..18]);

        var edited = appraisal.AddLandAndBuildingProperty();
        edited.LandDetail!.Update(ownerName: "Seed owner");
        edited.BuildingDetail!.Update(ownerName: "Seed owner");
        edited.BuildingDetail.AddDepreciationDetail("Gross", isBuilding: true, priceAfterDepreciation: 100_000m);
        edited.BuildingDetail.AddSurface(1, 1, "F");

        var other = appraisal.AddBuildingProperty();
        other.BuildingDetail!.Update(ownerName: "Other owner");
        other.BuildingDetail.AddDepreciationDetail("Gross", isBuilding: true, priceAfterDepreciation: 300_000m);

        db.Appraisals.Add(appraisal);
        await db.SaveChangesAsync(ct);
        return (appraisal.Id, edited.Id, other.Id);
    }

    private async Task<AppraisalProperty> ReloadAsync(Guid appraisalId, Guid propertyId, CancellationToken ct)
    {
        using var scope = CreateScope();
        var appraisal = await scope.ServiceProvider.GetRequiredService<IAppraisalRepository>()
            .GetByIdWithPropertiesAsync(appraisalId, ct);
        return appraisal!.GetProperty(propertyId)!;
    }

    private async Task SendAsync(IRequest<Unit> command, CancellationToken ct)
    {
        using var scope = CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, ct);
    }

    [Fact]
    public async Task Saving_one_property_leaves_the_others_untouched_and_does_not_reload_the_property_graph()
    {
        var ct = TestContext.Current.CancellationToken;
        var (appraisalId, editedId, otherId) = await SeedTwoBuildingsAsync(ct);

        var otherBefore = await ReloadAsync(appraisalId, otherId, ct);
        var otherStamp = otherBefore.UpdatedAt;
        var otherDepStamp = otherBefore.BuildingDetail!.DepreciationDetails.Single().UpdatedAt;
        var seeded = await ReloadAsync(appraisalId, editedId, ct);
        var keptDep = seeded.BuildingDetail!.DepreciationDetails.Single();

        using var sql = new SqlCapture();
        await SendAsync(new UpdateLandAndBuildingPropertyCommand(
            appraisalId, editedId,
            OwnerNameLand: "Land owner",
            OwnerNameBuilding: "Building owner",
            Titles: [new LandTitleItemData(null, "111", "DEED", Rai: 1m, Ngan: 0m, SquareWa: 0m)],
            DepreciationDetails:
            [
                new DepreciationItemData(keptDep.Id, "Gross", IsBuilding: true, PriceAfterDepreciation: 120_400m,
                    DepreciationPeriods: [new DepreciationPeriodItemData(1, 5, 2m, 10m, 1_000m)]),
                new DepreciationItemData(null, "Gross", IsBuilding: false, PriceAfterDepreciation: 999_999m),
            ],
            Surfaces: [new SurfaceItemData(null, 2, 3, "T")]), ct);
        var commands = sql.Commands.ToList();

        // The edit itself is saved: plain fields, an added title, an updated + an added depreciation row
        // with its periods, and the surface list replaced.
        var edited = await ReloadAsync(appraisalId, editedId, ct);
        Assert.Equal("Land owner", edited.LandDetail!.OwnerName);
        Assert.Equal("Building owner", edited.BuildingDetail!.OwnerName);
        Assert.Equal("111", Assert.Single(edited.LandDetail.Titles).TitleNumber);
        Assert.Equal(2, edited.BuildingDetail.DepreciationDetails.Count);
        var updatedDep = edited.BuildingDetail.DepreciationDetails.Single(d => d.Id == keptDep.Id);
        Assert.Equal(120_400m, updatedDep.PriceAfterDepreciation);
        Assert.Single(updatedDep.DepreciationPeriods);
        Assert.Equal(2, Assert.Single(edited.BuildingDetail.Surfaces).FromFloorNumber);

        // The property nobody edited keeps its audit stamp, and so do its child rows.
        var otherAfter = await ReloadAsync(appraisalId, otherId, ct);
        Assert.Equal(otherStamp, otherAfter.UpdatedAt);
        Assert.Equal(otherDepStamp, otherAfter.BuildingDetail!.DepreciationDetails.Single().UpdatedAt);

        // Insurance = IsBuilding rows only, rounded to 1,000, summed over both buildings.
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var row = await db.ValuationAnalyses.AsNoTracking().FirstAsync(v => v.AppraisalId == appraisalId, ct);
            Assert.Equal(RoundTo1000(120_400m) + 300_000m, row.InsuranceValue);
        }

        // The owned land titles are read once (the handler's load). A second read means RecomputeAsync
        // reloaded every property of the appraisal again.
        var titleReads = commands.Count(c => IsSelect(c) && c.Contains("[LandTitles]"));
        var appraisalUpdates = commands.Count(c => c.Contains("UPDATE [appraisal].[Appraisals]"));
        TestContext.Current.SendDiagnosticMessage(
            $"commands={commands.Count} landTitleSelects={titleReads} appraisalRootUpdates={appraisalUpdates}");
        Assert.Equal(1, titleReads);
        Assert.Equal(0, appraisalUpdates);
    }

    [Fact]
    public async Task A_second_save_removes_child_rows_that_are_no_longer_sent()
    {
        var ct = TestContext.Current.CancellationToken;
        var (appraisalId, editedId, _) = await SeedTwoBuildingsAsync(ct);
        var seeded = await ReloadAsync(appraisalId, editedId, ct);
        var keptDep = seeded.BuildingDetail!.DepreciationDetails.Single();

        await SendAsync(new UpdateLandAndBuildingPropertyCommand(
            appraisalId, editedId,
            DepreciationDetails:
            [
                new DepreciationItemData(keptDep.Id, "Gross", PriceAfterDepreciation: 100_000m,
                    DepreciationPeriods: [new DepreciationPeriodItemData(1, 5, 2m, 10m, 1_000m)]),
                new DepreciationItemData(null, "Gross", PriceAfterDepreciation: 50_000m),
            ],
            Surfaces: []), ct);

        var afterFirst = await ReloadAsync(appraisalId, editedId, ct);
        var added = afterFirst.BuildingDetail!.DepreciationDetails.Single(d => d.Id != keptDep.Id);

        // Drop the original row, keep the added one with a new period set.
        await SendAsync(new UpdateLandAndBuildingPropertyCommand(
            appraisalId, editedId,
            DepreciationDetails:
            [
                new DepreciationItemData(added.Id, "Gross", PriceAfterDepreciation: 60_000m,
                    DepreciationPeriods:
                    [
                        new DepreciationPeriodItemData(1, 2, 1m, 2m, 100m),
                        new DepreciationPeriodItemData(3, 4, 1m, 4m, 200m),
                    ]),
            ],
            Surfaces: []), ct);

        var afterSecond = await ReloadAsync(appraisalId, editedId, ct);
        var only = Assert.Single(afterSecond.BuildingDetail!.DepreciationDetails);
        Assert.Equal(added.Id, only.Id);
        Assert.Equal(60_000m, only.PriceAfterDepreciation);
        Assert.Equal(2, only.DepreciationPeriods.Count);
        Assert.Empty(afterSecond.BuildingDetail.Surfaces);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        Assert.Equal(0, await db.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM appraisal.BuildingDepreciationDetails WHERE Id = {keptDep.Id}")
            .SingleAsync(ct));
    }

    [Fact]
    public async Task Condo_save_syncs_area_details_without_touching_the_other_property()
    {
        var ct = TestContext.Current.CancellationToken;
        Guid appraisalId, condoId, otherId;
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
            appraisal.SetAppraisalNumber($"PSW-{Guid.NewGuid():N}"[..18]);
            var condo = appraisal.AddCondoProperty();
            condo.CondoDetail!.Update(ownerName: "Owner", address: Address.Create("Sub", "Dist", "Prov"), landOffice: "LO");
            condo.CondoDetail.AddCondoAreaDetail(CondoAppraisalAreaDetail.Create(1, "old", 10m));
            var other = appraisal.AddBuildingProperty();
            other.BuildingDetail!.Update(ownerName: "Other owner");
            db.Appraisals.Add(appraisal);
            await db.SaveChangesAsync(ct);
            (appraisalId, condoId, otherId) = (appraisal.Id, condo.Id, other.Id);
        }

        var otherStamp = (await ReloadAsync(appraisalId, otherId, ct)).UpdatedAt;

        await SendAsync(new UpdateCondoPropertyCommand(
            appraisalId, condoId,
            OwnerName: "New owner",
            AreaDetails: [new CondoAppraisalAreaDetailDto(null, 1, "living", 30m)]), ct);

        var condoAfter = await ReloadAsync(appraisalId, condoId, ct);
        Assert.Equal("New owner", condoAfter.CondoDetail!.OwnerName);
        Assert.Equal("living", Assert.Single(condoAfter.CondoDetail.AreaDetails).AreaDescription);
        Assert.Equal(otherStamp, (await ReloadAsync(appraisalId, otherId, ct)).UpdatedAt);
    }

    [Fact]
    public async Task Creating_a_property_group_is_saved_by_change_tracking_alone()
    {
        var ct = TestContext.Current.CancellationToken;
        Guid appraisalId;
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
            appraisal.SetAppraisalNumber($"PSW-{Guid.NewGuid():N}"[..18]);
            db.Appraisals.Add(appraisal);
            await db.SaveChangesAsync(ct);
            appraisalId = appraisal.Id;
        }

        CreatePropertyGroupResult result;
        using (var scope = CreateScope())
            result = await scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new CreatePropertyGroupCommand(appraisalId, "Group A"), ct);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var appraisal = await db.Appraisals.AsNoTracking().SingleAsync(a => a.Id == appraisalId, ct);
            var group = Assert.Single(appraisal.Groups, g => g.GroupName == "Group A");
            Assert.Equal(result.Id, group.Id);
        }
    }

    private static bool IsSelect(string sql)
        => sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase);
}
