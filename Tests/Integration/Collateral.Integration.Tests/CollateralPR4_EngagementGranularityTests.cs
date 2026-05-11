using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Collateral.CollateralMasters.Models;
using Collateral.CollateralMasters.Services;
using Collateral.Data;
using Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Collateral.Integration.Tests;

/// <summary>
/// PR-4 integration tests covering:
///   1. Single group, single property  — one engagement row per appraisal.
///   2. Single group, multi-title land — one IsMaster + alias masters; single engagement anchored to IsMaster.
///   3. Multi-group appraisal          — two groups → two IsMasters; ONLY the primary-group IsMaster gets the engagement.
///   4. Mixed type (Land + Condo)      — each group gets its own IsMaster; one engagement total.
///   5. Re-appraisal same composition  — running ProcessAppraisalAsync twice is idempotent (no duplicate engagement).
///   6. Alias-alone graceful behavior  — alias property whose parent IsMaster is absent → service succeeds, engagement
///      attaches to parent IsMaster (PR-7: validation moved upstream to Request module).
///   7. Snapshot shape                 — engagement.Snapshot contains groups[] array with correct structure.
///   8. Unique index                   — only one engagement per appraisal at the DB level.
/// </summary>
[Collection("Integration")]
public class CollateralPR4_EngagementGranularityTests(IntegrationTestFixture fixture)
{
    // -----------------------------------------------------------------------
    // Seed helpers
    // -----------------------------------------------------------------------

    private static AppraisalAggregate CreateAppraisalSeed(Guid requestId)
    {
        var a = AppraisalAggregate.Create(requestId, "New", "Normal");
        a.SetAppraisalNumber($"AP-PR4-{Guid.NewGuid():N}"[..18]);
        typeof(AppraisalAggregate)
            .GetProperty("CompletedAt")!
            .SetValue(a, DateTime.UtcNow);
        return a;
    }

    private static AppraisalProperty SeedLandProperty(
        AppraisalAggregate appraisal,
        string landOffice, string province, string district, string subDistrict,
        string titleNo, string titleType)
    {
        var prop = appraisal.AddLandProperty();
        prop.LandDetail!.Update(
            address: AdministrativeAddress.Create(subDistrict, district, province, landOffice));
        var title = LandTitle.Create(prop.LandDetail.Id, titleNo, titleType);
        prop.LandDetail.AddTitle(title);
        return prop;
    }

    private static AppraisalProperty SeedCondoProperty(
        AppraisalAggregate appraisal,
        string landOffice, string condoRegNo, string building, string floor, string room,
        string titleNo, string titleType, string province)
    {
        var prop = appraisal.AddCondoProperty();
        prop.CondoDetail!.Update(
            condoRegistrationNumber: condoRegNo,
            buildingNumber: building,
            floorNumber: floor,
            roomNumber: room,
            titleNumber: titleNo,
            titleType: titleType,
            ownerName: "Test Owner",
            address: AdministrativeAddress.Create(null, null, province, landOffice));
        return prop;
    }

    private IServiceScope CreateScope()
        => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    private CollateralDbContext GetCollateralDbContext(IServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<CollateralDbContext>();

    private AppraisalDbContext GetAppraisalDbContext(IServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

    private async Task ProcessAppraisalInNewScopeAsync(Guid appraisalId)
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICollateralMasterUpsertService>();
        await svc.ProcessAppraisalAsync(appraisalId, TestContext.Current.CancellationToken);
    }

    // -----------------------------------------------------------------------
    // PR4-1: Single group, single property — exactly one engagement per appraisal
    // -----------------------------------------------------------------------
    [Fact]
    public async Task PR4_1_SingleGroupSingleProperty_ProducesExactlyOneEngagement()
    {
        var titleNo = "PR4-1-" + Guid.NewGuid().ToString("N")[..8];
        Guid appraisalId;

        using (var seed = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seed);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandProperty(a, "LO-001", "Bangkok", "Bangrak", "Silom", titleNo, "Chanote");
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;
        }

        await ProcessAppraisalInNewScopeAsync(appraisalId);

        using var assert = CreateScope();
        var db = GetCollateralDbContext(assert);

        var master = await db.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .FirstAsync(m => m.LandDetail != null && m.LandDetail.TitleNumber == titleNo,
                TestContext.Current.CancellationToken);

        Assert.True(master.IsMaster, "Property should be created as IsMaster");
        Assert.Single(master.Engagements);
        Assert.Equal(appraisalId, master.Engagements.Single().AppraisalId);
    }

    // -----------------------------------------------------------------------
    // PR4-2: Single group, two land titles — one IsMaster + one alias; one engagement.
    //        With cost-approach pricing seeded, alias.LandDetail.UnitPrice must be propagated.
    //        (Regression guard for BLOCKER 1: newly-created aliases must get UnitPrice.)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task PR4_2_SingleGroupMultiTitle_OneMasterOneAliasOneEngagement()
    {
        var title1 = "PR4-2A-" + Guid.NewGuid().ToString("N")[..7];
        var title2 = "PR4-2B-" + Guid.NewGuid().ToString("N")[..7];
        const decimal expectedUnitPrice = 15_000m;
        Guid appraisalId;
        Guid prop1Id, prop2Id;

        using (var seed = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seed);
            var a = CreateAppraisalSeed(Guid.NewGuid());

            // Two land properties in the SAME group
            var g = a.CreateGroup("Group A");
            var p1 = SeedLandProperty(a, "LO-001", "Bangkok", "Bangrak", "Silom", title1, "Chanote");
            var p2 = SeedLandProperty(a, "LO-001", "Bangkok", "Bangrak", "Silom", title2, "NorSor4Jor");
            g.AddProperty(p1.Id);
            g.AddProperty(p2.Id);

            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;
            prop1Id = p1.Id;
            prop2Id = p2.Id;

            // Seed cost-approach pricing so UnitPrice propagation can be asserted.
            // FinalValueAdjusted → UnitPrice on IsMaster + alias (BLOCKER 1 regression guard).
            var pa = SeedCostApproachPricing(g.Id, expectedUnitPrice, buildingCost: 200_000m, appraisalPrice: 1_000_000m);
            appraisalDb.PricingAnalyses.Add(pa);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await ProcessAppraisalInNewScopeAsync(appraisalId);

        using var assert = CreateScope();
        var db = GetCollateralDbContext(assert);

        var masters = await db.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .Where(m => m.LandDetail != null &&
                        (m.LandDetail.TitleNumber == title1 || m.LandDetail.TitleNumber == title2))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, masters.Count);

        var isMaster = masters.Single(m => m.IsMaster);
        var alias = masters.Single(m => !m.IsMaster);

        // Alias must point back to IsMaster
        Assert.Equal(isMaster.Id, alias.ParentMasterId);

        // Engagement attaches to IsMaster only — alias has none
        Assert.Single(isMaster.Engagements);
        Assert.Empty(alias.Engagements);
        Assert.Equal(appraisalId, isMaster.Engagements.Single().AppraisalId);

        // BLOCKER 1 regression: newly-created alias must have UnitPrice propagated
        // (EF Core queries skip Added-but-unsaved entities; the fix tracks newAliases in memory).
        Assert.Equal(expectedUnitPrice, isMaster.LandDetail!.UnitPrice);
        Assert.Equal(expectedUnitPrice, alias.LandDetail!.UnitPrice);
        // BuildingCost and AppraisalValue are IsMaster-only; alias must have null
        Assert.Null(alias.LandDetail.BuildingCost);
        Assert.Null(alias.LandDetail.AppraisalValue);
    }

    // -----------------------------------------------------------------------
    // Pricing seed helper (mirrors PR8 test helper for cross-test reuse)
    // -----------------------------------------------------------------------
    private static PricingAnalysis SeedCostApproachPricing(
        Guid propertyGroupId,
        decimal finalValueAdjusted,
        decimal buildingCost,
        decimal appraisalPrice)
    {
        var pa = PricingAnalysis.CreateForPropertyGroup(propertyGroupId);
        pa.StartProgress();

        var approach = pa.AddApproach("Cost");
        approach.Select();

        var method = approach.AddMethod("WQS");
        method.SetAsSelected();
        method.SetValue(appraisalPrice);

        var fv = PricingFinalValue.Create(method.Id, finalValueAdjusted, appraisalPrice);
        fv.SetBuildingCost(buildingCost);
        fv.SetAppraisalPrice(appraisalPrice);
        method.SetFinalValue(fv);

        pa.SetFinalValues(appraisalPrice);
        return pa;
    }

    // -----------------------------------------------------------------------
    // PR4-3: Multi-group appraisal — primary group's IsMaster holds the engagement
    // -----------------------------------------------------------------------
    [Fact]
    public async Task PR4_3_MultiGroup_PrimaryGroupIsMasterHoldsEngagement()
    {
        var title1 = "PR4-3A-" + Guid.NewGuid().ToString("N")[..7];
        var title2 = "PR4-3B-" + Guid.NewGuid().ToString("N")[..7];
        Guid appraisalId;

        using (var seed = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seed);
            var a = CreateAppraisalSeed(Guid.NewGuid());

            // Group 1 (lower GroupNumber = primary)
            var g1 = a.CreateGroup("Group 1");
            var p1 = SeedLandProperty(a, "LO-001", "Bangkok", "Bangrak", "Silom", title1, "Chanote");
            g1.AddProperty(p1.Id);

            // Group 2 (higher GroupNumber = secondary)
            var g2 = a.CreateGroup("Group 2");
            var p2 = SeedLandProperty(a, "LO-002", "Chiang Mai", "Mueang", "Chang Phueak", title2, "Chanote");
            g2.AddProperty(p2.Id);

            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;
        }

        await ProcessAppraisalInNewScopeAsync(appraisalId);

        using var assert = CreateScope();
        var db = GetCollateralDbContext(assert);

        var master1 = await db.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .FirstAsync(m => m.LandDetail != null && m.LandDetail.TitleNumber == title1,
                TestContext.Current.CancellationToken);

        var master2 = await db.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .FirstAsync(m => m.LandDetail != null && m.LandDetail.TitleNumber == title2,
                TestContext.Current.CancellationToken);

        // Both created as IsMaster (different groups)
        Assert.True(master1.IsMaster);
        Assert.True(master2.IsMaster);

        // Only one engagement total; it sits on the primary (group 1) IsMaster
        Assert.Single(master1.Engagements);
        Assert.Empty(master2.Engagements);
        Assert.Equal(appraisalId, master1.Engagements.Single().AppraisalId);
    }

    // -----------------------------------------------------------------------
    // PR4-4: Mixed type (Land + Condo in separate groups) — one engagement total
    // -----------------------------------------------------------------------
    [Fact]
    public async Task PR4_4_MixedType_LandAndCondo_OneEngagementTotal()
    {
        var titleLand = "PR4-4L-" + Guid.NewGuid().ToString("N")[..7];
        var titleCondo = "PR4-4C-" + Guid.NewGuid().ToString("N")[..7];
        Guid appraisalId;

        using (var seed = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seed);
            var a = CreateAppraisalSeed(Guid.NewGuid());

            // Group 1: Land (primary)
            var g1 = a.CreateGroup("Land Group");
            var pLand = SeedLandProperty(a, "LO-001", "Bangkok", "Bangrak", "Silom", titleLand, "Chanote");
            g1.AddProperty(pLand.Id);

            // Group 2: Condo (secondary)
            var g2 = a.CreateGroup("Condo Group");
            var pCondo = SeedCondoProperty(a, "LO-002", "CONDO-REG-001", "A", "5", "501",
                titleCondo, "Chanote", "Bangkok");
            g2.AddProperty(pCondo.Id);

            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;
        }

        await ProcessAppraisalInNewScopeAsync(appraisalId);

        using var assert = CreateScope();
        var db = GetCollateralDbContext(assert);

        var landMaster = await db.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .FirstAsync(m => m.LandDetail != null && m.LandDetail.TitleNumber == titleLand,
                TestContext.Current.CancellationToken);

        var condoMaster = await db.CollateralMasters
            .Include(m => m.CondoDetail)
            .Include(m => m.Engagements)
            .FirstAsync(m => m.CondoDetail != null && m.CondoDetail.TitleNumber == titleCondo,
                TestContext.Current.CancellationToken);

        // Land group is primary — engagement attaches there
        Assert.Single(landMaster.Engagements);
        Assert.Empty(condoMaster.Engagements);
        Assert.Equal(appraisalId, landMaster.Engagements.Single().AppraisalId);
    }

    // -----------------------------------------------------------------------
    // PR4-5: Re-appraisal — processing the same AppraisalId twice is idempotent
    // -----------------------------------------------------------------------
    [Fact]
    public async Task PR4_5_ReAppraisal_SameAppraisalId_IsIdempotent()
    {
        var titleNo = "PR4-5-" + Guid.NewGuid().ToString("N")[..8];
        Guid appraisalId;

        using (var seed = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seed);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandProperty(a, "LO-001", "Bangkok", "Bangrak", "Silom", titleNo, "Chanote");
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;
        }

        // Process twice — second call must not throw
        await ProcessAppraisalInNewScopeAsync(appraisalId);
        await ProcessAppraisalInNewScopeAsync(appraisalId);

        using var assert = CreateScope();
        var db = GetCollateralDbContext(assert);

        var master = await db.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .FirstAsync(m => m.LandDetail != null && m.LandDetail.TitleNumber == titleNo,
                TestContext.Current.CancellationToken);

        // Still exactly one engagement — no duplicate
        Assert.Single(master.Engagements);
    }

    // -----------------------------------------------------------------------
    // PR4-6: Alias-alone graceful behavior (PR-7: validation moved upstream)
    //
    // An alias land title appraised alone (without its parent IsMaster title)
    // must no longer be rejected. The service resolves the alias to its parent
    // IsMaster and attaches the engagement there. IsMaster classification is
    // unchanged after the operation.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task PR4_6_AliasAlone_GracefullyAttachesEngagementToParentIsMaster()
    {
        // First appraisal: two land titles in the same group.
        // One becomes IsMaster, the other becomes an alias.
        var title1 = "PR4-6A-" + Guid.NewGuid().ToString("N")[..7];
        var title2 = "PR4-6B-" + Guid.NewGuid().ToString("N")[..7];
        Guid appraisalId1;

        using (var seed = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seed);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            var g = a.CreateGroup("Group AB");
            var p1 = SeedLandProperty(a, "LO-001", "Bangkok", "Bangrak", "Silom", title1, "Chanote");
            var p2 = SeedLandProperty(a, "LO-001", "Bangkok", "Bangrak", "Silom", title2, "NorSor4Jor");
            g.AddProperty(p1.Id);
            g.AddProperty(p2.Id);
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId1 = a.Id;
        }

        // Process first appraisal — one title becomes IsMaster, the other an alias.
        await ProcessAppraisalInNewScopeAsync(appraisalId1);

        // Capture the IsMaster row created by appraisal #1.
        Guid isMasterId;
        using (var snap = CreateScope())
        {
            var db = GetCollateralDbContext(snap);
            var masters = await db.CollateralMasters
                .Include(m => m.LandDetail)
                .Where(m => m.LandDetail != null &&
                    (m.LandDetail.TitleNumber == title1 || m.LandDetail.TitleNumber == title2))
                .ToListAsync(TestContext.Current.CancellationToken);

            var isMasterRow = masters.SingleOrDefault(m => m.IsMaster);
            Assert.NotNull(isMasterRow);
            isMasterId = isMasterRow.Id;
        }

        // Second appraisal: only the alias title (title2), parent IsMaster (title1) is absent.
        Guid appraisalId2;
        using (var seed = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seed);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandProperty(a, "LO-001", "Bangkok", "Bangrak", "Silom", title2, "NorSor4Jor");
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId2 = a.Id;
        }

        // Must NOT throw — the land path resolves alias → parent IsMaster and proceeds.
        await ProcessAppraisalInNewScopeAsync(appraisalId2);

        // Engagement for appraisal #2 attaches to the parent IsMaster.
        using var assert = CreateScope();
        var assertDb = GetCollateralDbContext(assert);

        var engagementsForAppraisal2 = await assertDb.CollateralEngagements
            .Where(e => e.AppraisalId == appraisalId2)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(engagementsForAppraisal2);
        Assert.Equal(isMasterId, engagementsForAppraisal2.Single().CollateralMasterId);

        // IsMaster classification must be unchanged.
        var isMasterAfter = await assertDb.CollateralMasters
            .FirstAsync(m => m.Id == isMasterId, TestContext.Current.CancellationToken);

        Assert.True(isMasterAfter.IsMaster, "IsMaster classification must not have changed");
        Assert.Null(isMasterAfter.ParentMasterId);
    }

    // -----------------------------------------------------------------------
    // PR4-7: Snapshot shape — groups[] array with required keys.
    //        For a single-title group: one property entry with role="isMaster" and collateralMasterId.
    //        For a two-title group:    two entries — one role="isMaster", one role="alias",
    //                                  each with a non-empty collateralMasterId. (Fix: MAJOR 2 + 3)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task PR4_7_SnapshotShape_ContainsGroupsArray()
    {
        var titleNo = "PR4-7-" + Guid.NewGuid().ToString("N")[..8];
        Guid appraisalId;

        using (var seed = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seed);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandProperty(a, "LO-001", "Bangkok", "Bangrak", "Silom", titleNo, "Chanote");
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;
        }

        await ProcessAppraisalInNewScopeAsync(appraisalId);

        using var assert = CreateScope();
        var db = GetCollateralDbContext(assert);

        var master = await db.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .FirstAsync(m => m.LandDetail != null && m.LandDetail.TitleNumber == titleNo,
                TestContext.Current.CancellationToken);

        var snapshot = master.Engagements.Single().Snapshot;
        Assert.NotNull(snapshot);

        using var doc = JsonDocument.Parse(snapshot);
        var root = doc.RootElement;

        // Top-level must have groups[]
        Assert.True(root.TryGetProperty("groups", out var groups), "Snapshot must have 'groups' key");
        Assert.Equal(JsonValueKind.Array, groups.ValueKind);

        var firstGroup = groups.EnumerateArray().First();

        // Required group-level keys
        Assert.True(firstGroup.TryGetProperty("isMasterId", out _), "Group must have 'isMasterId'");
        Assert.True(firstGroup.TryGetProperty("isPrimary", out var isPrimaryEl), "Group must have 'isPrimary'");
        Assert.Equal(JsonValueKind.True, isPrimaryEl.ValueKind); // only group → primary

        Assert.True(firstGroup.TryGetProperty("properties", out var propsEl), "Group must have 'properties'");
        Assert.Equal(JsonValueKind.Array, propsEl.ValueKind);
        Assert.True(propsEl.GetArrayLength() > 0, "Group.properties must be non-empty");

        // constructionInspections[] must be present (may be empty)
        Assert.True(firstGroup.TryGetProperty("constructionInspections", out var ciEl), "Group must have 'constructionInspections'");
        Assert.Equal(JsonValueKind.Array, ciEl.ValueKind);

        // Property entry must identify itself and carry collateralMasterId (MAJOR 3)
        var firstProp = propsEl.EnumerateArray().First();
        Assert.True(firstProp.TryGetProperty("propertyId", out _), "Property entry must have 'propertyId'");
        Assert.True(firstProp.TryGetProperty("role", out var roleEl), "Property entry must have 'role'");
        Assert.True(firstProp.TryGetProperty("type", out _), "Property entry must have 'type'");
        Assert.True(firstProp.TryGetProperty("collateralMasterId", out var cmIdEl),
            "Property entry must have 'collateralMasterId' (MAJOR 3)");
        Assert.False(string.IsNullOrWhiteSpace(cmIdEl.GetString()),
            "collateralMasterId must be non-empty");

        // isMasterId on group must match master.Id
        Assert.Equal(master.Id.ToString(), firstGroup.GetProperty("isMasterId").GetString());
        // Single-title → exactly one property entry with role="isMaster"
        Assert.Equal("isMaster", roleEl.GetString());
    }

    // -----------------------------------------------------------------------
    // PR4-7b: Snapshot shape for multi-title group — one entry per CollateralMaster
    //         (one "isMaster" + one "alias"), each with a non-empty collateralMasterId.
    //         Verifies MAJOR 2 (alias entries in snapshot) and MAJOR 3 (collateralMasterId).
    // -----------------------------------------------------------------------
    [Fact]
    public async Task PR4_7b_SnapshotShape_MultiTitleGroup_OneEntryPerMaster()
    {
        var title1 = "PR4-7b1-" + Guid.NewGuid().ToString("N")[..6];
        var title2 = "PR4-7b2-" + Guid.NewGuid().ToString("N")[..6];
        Guid appraisalId;

        using (var seed = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seed);
            var a = CreateAppraisalSeed(Guid.NewGuid());

            var g = a.CreateGroup("MultiTitle Group");
            var p1 = SeedLandProperty(a, "LO-001", "Bangkok", "Bangrak", "Silom", title1, "Chanote");
            var p2 = SeedLandProperty(a, "LO-001", "Bangkok", "Bangrak", "Silom", title2, "NorSor4Jor");
            g.AddProperty(p1.Id);
            g.AddProperty(p2.Id);

            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;
        }

        await ProcessAppraisalInNewScopeAsync(appraisalId);

        using var assert = CreateScope();
        var db = GetCollateralDbContext(assert);

        var isMasterRow = await db.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .FirstAsync(m => m.LandDetail != null &&
                             (m.LandDetail.TitleNumber == title1 || m.LandDetail.TitleNumber == title2)
                             && m.IsMaster,
                TestContext.Current.CancellationToken);

        var snapshot = isMasterRow.Engagements.Single().Snapshot;
        Assert.NotNull(snapshot);

        using var doc = JsonDocument.Parse(snapshot);
        var root = doc.RootElement;
        var firstGroup = root.GetProperty("groups").EnumerateArray().First();
        var propsEl = firstGroup.GetProperty("properties");

        // Two titles → two entries (one IsMaster + one alias) (MAJOR 2)
        Assert.Equal(2, propsEl.GetArrayLength());

        var propsList = propsEl.EnumerateArray().ToList();
        var roles = propsList.Select(p => p.GetProperty("role").GetString()).ToHashSet();
        Assert.Contains("isMaster", roles);
        Assert.Contains("alias", roles);

        // Every entry must have a non-empty collateralMasterId (MAJOR 3)
        foreach (var entry in propsList)
        {
            Assert.True(entry.TryGetProperty("collateralMasterId", out var cmIdEl),
                "Each property entry must have 'collateralMasterId'");
            Assert.False(string.IsNullOrWhiteSpace(cmIdEl.GetString()),
                "collateralMasterId must be non-empty");
        }

        // The IsMaster entry's collateralMasterId must match the actual IsMaster row
        var isMasterEntry = propsList.Single(p => p.GetProperty("role").GetString() == "isMaster");
        Assert.Equal(isMasterRow.Id.ToString(),
            isMasterEntry.GetProperty("collateralMasterId").GetString());
    }

    // -----------------------------------------------------------------------
    // PR4-8: Unique index — second appraisal produces its own engagement row
    //         (the unique index is on AppraisalId, not on PropertyId)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task PR4_8_UniqueIndex_TwoAppraisals_TwoSeparateEngagements()
    {
        var titleNo = "PR4-8-" + Guid.NewGuid().ToString("N")[..8];
        Guid appraisalId1, appraisalId2;

        using (var seed = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seed);

            var a1 = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandProperty(a1, "LO-001", "Bangkok", "Bangrak", "Silom", titleNo, "Chanote");

            var a2 = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandProperty(a2, "LO-001", "Bangkok", "Bangrak", "Silom", titleNo, "Chanote");

            appraisalDb.Appraisals.AddRange(a1, a2);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId1 = a1.Id;
            appraisalId2 = a2.Id;
        }

        await ProcessAppraisalInNewScopeAsync(appraisalId1);
        await ProcessAppraisalInNewScopeAsync(appraisalId2);

        using var assert = CreateScope();
        var db = GetCollateralDbContext(assert);

        var master = await db.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .FirstAsync(m => m.LandDetail != null && m.LandDetail.TitleNumber == titleNo,
                TestContext.Current.CancellationToken);

        // Two distinct appraisals → two engagement rows on the same IsMaster
        Assert.Equal(2, master.Engagements.Count);

        var appraisalIds = master.Engagements.Select(e => e.AppraisalId).ToHashSet();
        Assert.Contains(appraisalId1, appraisalIds);
        Assert.Contains(appraisalId2, appraisalIds);
    }
}
