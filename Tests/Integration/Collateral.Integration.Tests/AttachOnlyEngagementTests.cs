using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Collateral.CollateralMasters.Exceptions;
using Collateral.CollateralMasters.Services;
using Collateral.Contracts;
using Collateral.Data;
using Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;
using Address = Appraisal.Domain.Appraisals.Address;

namespace Integration.Collateral.Integration.Tests;

/// <summary>
/// The attach-only branch: an appraisal that describes no collateral of its own joins the master its
/// chain already owns.
///
/// Construction-inspection appraisals routinely record only the BUILDING — or nothing at all — because
/// the inspector does not re-enter the land. 'B' is not a collateral type, so such an appraisal formed
/// no property group, ran no upsert, never queried the dedup key, and so never reached the chain
/// fallback either (the fallback only fires on a dedup MISS). It ended up with no engagement at all,
/// and the construction progress never reached the collateral module: vw_RegulatoryExport reported
/// IsUnderConstruction = 0 on all 45,683 U3 rows while 177 inspections sat below 100%.
///
/// The rule these tests pin, agreed with the business: ATTACH to the master the chain already owns,
/// and NEVER create one. Half of the assertions here exist to prove the second half of that sentence.
/// </summary>
[Collection("Integration")]
public class AttachOnlyEngagementTests(IntegrationTestFixture fixture)
{
    private IServiceScope CreateScope()
        => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    private static AppraisalAggregate NewAppraisal(string appraisalType, Guid? prevAppraisalId)
    {
        var a = AppraisalAggregate.Create(
            Guid.NewGuid(), appraisalType, "Normal", DateTime.Now, prevAppraisalId: prevAppraisalId);
        a.SetAppraisalNumber($"AT-{Guid.NewGuid():N}"[..18]);
        // CompletedAt is set directly: the collateral write path only runs for completed appraisals,
        // and driving the real status transitions here would test the Appraisal module instead.
        typeof(AppraisalAggregate).GetProperty("CompletedAt")!.SetValue(a, DateTime.UtcNow);
        return a;
    }

    private async Task<Guid> ProcessAsync(AppraisalAggregate a)
    {
        using (var seedScope = CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            db.Appraisals.Add(a);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Fresh scope, mirroring the consumer: each message gets its own DbContext.
        using var runScope = CreateScope();
        await runScope.ServiceProvider.GetRequiredService<ICollateralMasterUpsertService>()
            .ProcessAppraisalAsync(a.Id, TestContext.Current.CancellationToken);

        return a.Id;
    }

    /// <summary>A completed land appraisal — yields a bare-land 'L' master.</summary>
    private Task<Guid> SeedLandAsync(string titleNo, string subDistrict, Guid? prev = null)
    {
        var a = NewAppraisal("New", prev);
        var prop = a.AddLandProperty();
        prop.LandDetail!.Update(
            address: Address.Create(subDistrict, "D-ATTACH", "BKK"), landOffice: "LO-ATTACH");
        prop.LandDetail.AddTitle(LandTitle.Create(prop.LandDetail.Id, titleNo, "Chanote"));
        return ProcessAsync(a);
    }

    /// <summary>A completed land appraisal that also carries a building — yields an 'LB' master.</summary>
    private Task<Guid> SeedLandWithBuildingAsync(string titleNo, string subDistrict, Guid? prev = null)
    {
        var a = NewAppraisal("New", prev);
        var land = a.AddLandProperty();
        land.LandDetail!.Update(
            address: Address.Create(subDistrict, "D-ATTACH", "BKK"), landOffice: "LO-ATTACH");
        land.LandDetail.AddTitle(LandTitle.Create(land.LandDetail.Id, titleNo, "Chanote"));

        var building = a.AddBuildingProperty();
        building.BuildingDetail!.Update(buildingType: "01", totalBuildingArea: 120m);
        return ProcessAsync(a);
    }

    /// <summary>
    /// A construction-inspection appraisal exactly as the inspectors record it: one BUILDING property,
    /// no land. Optionally part-built, which is what has to reach the collateral engagement.
    /// </summary>
    private Task<Guid> SeedBuildingOnlyAsync(
        Guid? prev, decimal? inspectedTotal = null, decimal? inspectedCurrent = null)
    {
        var a = NewAppraisal("Progressive", prev);
        var building = a.AddBuildingProperty();
        building.BuildingDetail!.Update(buildingType: "01", totalBuildingArea: 120m);

        if (inspectedTotal is not null)
            building.SetConstructionInspection(ConstructionInspection.CreateSummary(
                building.Id,
                totalValue: inspectedTotal.Value,
                summaryDetail: null,
                summaryPreviousProgressPct: 0m,
                summaryPreviousValue: 0m,
                summaryCurrentProgressPct: inspectedCurrent / inspectedTotal * 100m,
                summaryCurrentValue: inspectedCurrent,
                remark: null));

        return ProcessAsync(a);
    }

    /// <summary>An appraisal with no property rows at all — the other half of the attach-only case.</summary>
    private Task<Guid> SeedNoPropertyAsync(Guid? prev)
        => ProcessAsync(NewAppraisal("Progressive", prev));

    private async Task<CollateralEngagementProbe?> EngagementOfAsync(Guid appraisalId)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollateralDbContext>();
        return await db.CollateralEngagements.AsNoTracking()
            .Where(e => e.AppraisalId == appraisalId)
            .Select(e => new CollateralEngagementProbe(
                e.CollateralMasterId,
                e.IsUnderConstruction,
                e.ConstructionProgressPercent,
                e.Buildings.Count))
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
    }

    private async Task<string?> MasterTypeAsync(Guid masterId)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollateralDbContext>();
        return await db.CollateralMasters.AsNoTracking()
            .Where(m => m.Id == masterId)
            .Select(m => m.CollateralType)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
    }

    private async Task<int> MasterCountAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollateralDbContext>();
        return await db.CollateralMasters.AsNoTracking()
            .CountAsync(TestContext.Current.CancellationToken);
    }

    private sealed record CollateralEngagementProbe(
        Guid CollateralMasterId, bool? IsUnderConstruction, decimal? ProgressPercent, int BuildingCount);

    // ── The bug this branch exists for ────────────────────────────────────────

    /// <summary>
    /// The headline case: a part-built inspection recorded as building-only now reaches the engagement,
    /// carrying the flag and the percentage the regulatory export reads.
    /// </summary>
    [Fact]
    public async Task BuildingOnlyInspection_AttachesToChainMaster_AndCarriesConstructionProgress()
    {
        var firstId = await SeedLandWithBuildingAsync($"AT-{Guid.NewGuid():N}"[..16], "S-PROGRESS");
        var firstEngagement = await EngagementOfAsync(firstId);
        Assert.NotNull(firstEngagement);

        // 1,000 of finished value, 100 built so far → under construction at 10%.
        var inspectionId = await SeedBuildingOnlyAsync(firstId, inspectedTotal: 1000m, inspectedCurrent: 100m);

        var attached = await EngagementOfAsync(inspectionId);
        Assert.NotNull(attached);
        Assert.Equal(firstEngagement.CollateralMasterId, attached.CollateralMasterId);
        Assert.True(attached.IsUnderConstruction);
        Assert.Equal(10m, attached.ProgressPercent);
        Assert.Equal(1, attached.BuildingCount);
    }

    /// <summary>
    /// The reason the fallback walks the chain instead of reading PrevAppraisalId alone. Inspections
    /// come in runs, and the ones in between own no master, so the nearest master can be several hops
    /// up — 34 at the worst on U3. Reading one hop resolved 46 of 93 part-built chain tips; walking
    /// resolves all 93.
    /// </summary>
    [Fact]
    public async Task BuildingOnlyInspection_WalksPastAncestorsThatOwnNoMaster()
    {
        var landId = await SeedLandWithBuildingAsync($"AT-{Guid.NewGuid():N}"[..16], "S-WALK");
        var masterId = (await EngagementOfAsync(landId))!.CollateralMasterId;

        // Two intervening appraisals with no property at all — neither can own a master.
        var gapA = await SeedNoPropertyAsync(landId);
        var gapB = await SeedNoPropertyAsync(gapA);

        var inspectionId = await SeedBuildingOnlyAsync(gapB, inspectedTotal: 500m, inspectedCurrent: 250m);

        var attached = await EngagementOfAsync(inspectionId);
        Assert.NotNull(attached);
        Assert.Equal(masterId, attached.CollateralMasterId);
        Assert.Equal(50m, attached.ProgressPercent);
    }

    /// <summary>
    /// An appraisal with no property rows at all still earns its engagement: it carries this chain's
    /// latest appraisal number, date and value, which is what the regulatory export reports. It gets no
    /// building rows, because it describes no building.
    /// </summary>
    [Fact]
    public async Task AppraisalWithNoPropertyAtAll_AttachesWithNoBuildingRows()
    {
        var landId = await SeedLandAsync($"AT-{Guid.NewGuid():N}"[..16], "S-EMPTY");
        var masterId = (await EngagementOfAsync(landId))!.CollateralMasterId;

        var emptyId = await SeedNoPropertyAsync(landId);

        var attached = await EngagementOfAsync(emptyId);
        Assert.NotNull(attached);
        Assert.Equal(masterId, attached.CollateralMasterId);
        Assert.Equal(0, attached.BuildingCount);
    }

    // ── L → LB upgrade ────────────────────────────────────────────────────────

    /// <summary>
    /// Land appraised before the building started is typed 'L'. When the inspection records the
    /// building, the master has to become 'LB' — vw_RegulatoryExport short-circuits bare land to 0%
    /// BEFORE it reads IsUnderConstruction, so leaving it 'L' would report a 10%-built building as
    /// complete and throw away everything this branch just attached.
    /// </summary>
    [Fact]
    public async Task BuildingOnlyInspection_UpgradesBareLandMasterToLandWithBuilding()
    {
        var landId = await SeedLandAsync($"AT-{Guid.NewGuid():N}"[..16], "S-UPGRADE");
        var masterId = (await EngagementOfAsync(landId))!.CollateralMasterId;
        Assert.Equal(CollateralTypes.Land, await MasterTypeAsync(masterId));

        await SeedBuildingOnlyAsync(landId, inspectedTotal: 800m, inspectedCurrent: 80m);

        Assert.Equal(CollateralTypes.LandWithBuilding, await MasterTypeAsync(masterId));
    }

    /// <summary>
    /// Upgrade only. An inspection that records no building is NOT evidence that the building is gone —
    /// it only means this visit did not describe it. Downgrading would tell the regulator that a
    /// part-built structure is bare land.
    /// </summary>
    [Fact]
    public async Task AppraisalWithNoProperty_NeverDowngradesLandWithBuildingToLand()
    {
        var landId = await SeedLandWithBuildingAsync($"AT-{Guid.NewGuid():N}"[..16], "S-NODOWNGRADE");
        var masterId = (await EngagementOfAsync(landId))!.CollateralMasterId;
        Assert.Equal(CollateralTypes.LandWithBuilding, await MasterTypeAsync(masterId));

        await SeedNoPropertyAsync(landId);

        Assert.Equal(CollateralTypes.LandWithBuilding, await MasterTypeAsync(masterId));
    }

    /// <summary>Already 'LB' — the upgrade is a no-op, not a rewrite.</summary>
    [Fact]
    public async Task BuildingOnlyInspection_LeavesAnAlreadyLandWithBuildingMasterUntouched()
    {
        var landId = await SeedLandWithBuildingAsync($"AT-{Guid.NewGuid():N}"[..16], "S-NOOP");
        var masterId = (await EngagementOfAsync(landId))!.CollateralMasterId;

        await SeedBuildingOnlyAsync(landId, inspectedTotal: 300m, inspectedCurrent: 150m);

        Assert.Equal(CollateralTypes.LandWithBuilding, await MasterTypeAsync(masterId));
    }

    // ── The prohibition: never create a master ────────────────────────────────

    /// <summary>
    /// The whole point of the agreed rule. No ancestor owns a master, so there is nothing to attach to
    /// — and the branch must leave the table exactly as it found it rather than invent a collateral.
    /// A fabricated master is permanent: there is no merge or split tool.
    /// </summary>
    [Fact]
    public async Task BuildingOnlyInspection_WithNoAncestorMaster_CreatesNoMaster()
    {
        var before = await MasterCountAsync();

        // PrevAppraisalId points at an appraisal that itself owns nothing.
        var orphanAncestor = await SeedNoPropertyAsync(null);
        var inspectionId = await SeedBuildingOnlyAsync(orphanAncestor, inspectedTotal: 900m, inspectedCurrent: 90m);

        Assert.Equal(before, await MasterCountAsync());
        Assert.Null(await EngagementOfAsync(inspectionId));
        Assert.Null(await EngagementOfAsync(orphanAncestor));
    }

    /// <summary>A building-only inspection with no chain at all has nothing to attach to either.</summary>
    [Fact]
    public async Task BuildingOnlyInspection_WithNoPrevAppraisal_CreatesNoMaster()
    {
        var before = await MasterCountAsync();

        var inspectionId = await SeedBuildingOnlyAsync(null, inspectedTotal: 900m, inspectedCurrent: 90m);

        Assert.Equal(before, await MasterCountAsync());
        Assert.Null(await EngagementOfAsync(inspectionId));
    }

    /// <summary>
    /// The line the branch must not cross. This appraisal HAS a land property — it is simply missing
    /// the sub-district and title needed to identify it (about 5,672 rows on U3). That collateral is its
    /// own, not the predecessor's, so attaching it to the chain master could bind a different parcel for
    /// good, and there is no merge or split tool to undo that.
    ///
    /// ValidateAllProperties rejects it outright, BEFORE the attach-only branch is reached — so the
    /// boundary is enforced by an exception, not by attach-only declining. That is stricter than the
    /// guard this test was written to check, and it is the behaviour to pin: if validation were ever
    /// relaxed to "skip the property and carry on", the appraisal would arrive at attach-only with an
    /// empty inScopeProperties and would be swept up. The Count == 0 condition alone would not save it.
    /// </summary>
    [Fact]
    public async Task AppraisalWithLandPropertyButNoIdentity_IsRejected_NotAttached()
    {
        var landId = await SeedLandAsync($"AT-{Guid.NewGuid():N}"[..16], "S-GUARD");
        var masterId = (await EngagementOfAsync(landId))!.CollateralMasterId;
        var mastersBefore = await MasterCountAsync();

        // A land property whose identity cannot be resolved: no sub-district, no title.
        var a = NewAppraisal("Progressive", landId);
        var prop = a.AddLandProperty();
        prop.LandDetail!.Update(
            address: Address.Create(null, "D-ATTACH", "BKK"), landOffice: "LO-ATTACH");

        await Assert.ThrowsAsync<MissingIdentityKeyException>(() => ProcessAsync(a));

        // Nothing was attached to the predecessor's master, and nothing was created.
        Assert.Null(await EngagementOfAsync(a.Id));
        Assert.Equal(mastersBefore, await MasterCountAsync());
        Assert.Equal(CollateralTypes.Land, await MasterTypeAsync(masterId));
    }
}
