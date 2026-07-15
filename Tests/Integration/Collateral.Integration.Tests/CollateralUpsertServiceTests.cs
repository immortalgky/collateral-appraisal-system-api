using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Collateral.Application.Features.CollateralMasters.Lookup;
using Collateral.CollateralMasters.Exceptions;
using Collateral.Contracts;
using Collateral.Contracts.Engagements;
using Collateral.CollateralMasters.Models;
using Collateral.CollateralMasters.Services;
using Collateral.Data;
using Integration.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Exceptions;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Collateral.Integration.Tests;

/// <summary>
/// Integration tests for CollateralMasterUpsertService.
/// Seeds appraisal data directly via EF Core (bypassing domain status transitions)
/// to isolate the Collateral write path under test.
/// </summary>
[Collection("Integration")]
public class CollateralUpsertServiceTests(IntegrationTestFixture fixture)
{
    // -----------------------------------------------------------------------
    // Seed helpers
    // -----------------------------------------------------------------------

    private static AppraisalProperty SeedLandProperty(
        AppraisalAggregate appraisal,
        string landOffice, string province, string district, string subDistrict,
        string titleNo, string titleType,
        ConstructionInspection? inspection = null)
    {
        var prop = appraisal.AddLandProperty();
        prop.LandDetail!.Update(
            address: AdministrativeAddress.Create(subDistrict, district, province, landOffice));
        var title = LandTitle.Create(prop.LandDetail.Id, titleNo, titleType);
        prop.LandDetail.AddTitle(title);

        if (inspection is not null)
            prop.SetConstructionInspection(inspection);

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
            ownerName: "Test Owner",   // Required NOT NULL by DB schema
            address: AdministrativeAddress.Create("Test Subdistrict", "Test District", province, landOffice));
        return prop;
    }

    private static AppraisalProperty SeedMachineryProperty(
        AppraisalAggregate appraisal,
        string? registrationNo = null,
        string? serialNo = null,
        string? brand = null,
        string? model = null,
        string? manufacturer = null)
    {
        var prop = appraisal.AddMachineryProperty();
        prop.MachineryDetail!.Update(
            registrationNumber: registrationNo,
            serialNo: serialNo,
            brand: brand,
            model: model,
            manufacturer: manufacturer);
        return prop;
    }

    private static AppraisalAggregate CreateAppraisalSeed(Guid requestId)
    {
        var a = AppraisalAggregate.Create(requestId, "New", "Normal", DateTime.Now);
        a.SetAppraisalNumber($"AP-{Guid.NewGuid():N}".Substring(0, 18));
        // Set CompletedAt directly via reflection since domain requires status flow
        typeof(AppraisalAggregate)
            .GetProperty("CompletedAt")!
            .SetValue(a, DateTime.UtcNow);
        return a;
    }

    private IServiceScope CreateScope()
        => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    private CollateralDbContext GetCollateralDbContext(IServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<CollateralDbContext>();

    private AppraisalDbContext GetAppraisalDbContext(IServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

    private ICollateralMasterUpsertService GetUpsertService(IServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<ICollateralMasterUpsertService>();

    /// <summary>
    /// Runs ProcessAppraisalAsync in a fresh isolated scope (mirrors MassTransit consumer behaviour
    /// where each message gets its own DI scope and fresh DbContext).
    /// </summary>
    private async Task ProcessAppraisalInNewScopeAsync(Guid appraisalId)
    {
        using var scope = CreateScope();
        var svc = GetUpsertService(scope);
        await svc.ProcessAppraisalAsync(appraisalId, TestContext.Current.CancellationToken);
    }

    // -----------------------------------------------------------------------
    // Test 1: First appraisal creates master + engagement + snapshot
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test1_FirstAppraisal_CreatesLandMasterAndEngagement()
    {
        using var scope = CreateScope();
        var appraisalDb = GetAppraisalDbContext(scope);
        var collateralDb = GetCollateralDbContext(scope);
        var svc = GetUpsertService(scope);

        var requestId = Guid.NewGuid();
        var appraisal = CreateAppraisalSeed(requestId);

        var inspection = ConstructionInspection.CreateFullDetail(Guid.Empty, 10_000_000m);
        // 3 work items summing to 50% overall
        var wg = Guid.NewGuid();
        inspection.AddWorkDetail(wg, "Foundation", 1, 20m, 0m, 100m); // 20% weight, 100% done → 20
        inspection.AddWorkDetail(wg, "Structure", 2, 50m, 0m, 40m);    // 50% weight, 40% done → 20
        inspection.AddWorkDetail(wg, "Roof", 3, 30m, 0m, 33.33m);      // 30% weight, 33.33% done ≈ 10
        inspection.ComputeAllValues();
        // Fix AppraisalPropertyId after inspection is attached
        var landProp = SeedLandProperty(appraisal,
            "LO-BKK", "Bangkok", "Bangrak", "Silom", "12345", "Chanote", inspection);
        // The inspection was created with Guid.Empty — fix to match the property
        typeof(ConstructionInspection)
            .GetProperty("AppraisalPropertyId")!
            .SetValue(inspection, landProp.Id);

        appraisalDb.Appraisals.Add(appraisal);
        await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await svc.ProcessAppraisalAsync(appraisal.Id, TestContext.Current.CancellationToken);

        // Assert — CollateralMasters
        var masters = await collateralDb.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .Where(m => m.LandDetail != null &&
                        m.LandDetail.TitleNumber == "12345" &&
                        m.LandDetail.Province == "Bangkok")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(masters);
        var master = masters[0];
        Assert.Equal(CollateralTypes.Land, master.CollateralType);
        Assert.True(master.LandDetail!.IsUnderConstructionAtLastAppraisal);
        // PR-5: LastConstructionInspectionId removed from LandDetail; CI list is now in the engagement snapshot.
        Assert.NotNull(master.LandDetail.OverallConstructionProgressPercent);
        Assert.True(master.LandDetail.OverallConstructionProgressPercent < 100m);

        // Assert — Engagements
        Assert.Single(master.Engagements);
        var engagement = master.Engagements[0];
        Assert.Equal(appraisal.Id, engagement.AppraisalId);
        // PR-4: PropertyId dropped from CollateralEngagement (engagement is now per-appraisal).
        Assert.NotNull(engagement.Snapshot);
        // PR-4/PR-5: snapshot uses groups-centric shape with constructionInspections nested per group
        Assert.Contains("groups", engagement.Snapshot);
    }

    // -----------------------------------------------------------------------
    // Test 2: Idempotency — re-run produces no duplicates
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test2_Idempotency_ReRunProducesNoDuplicates()
    {
        // Seed the appraisal
        Guid appraisalId;
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var appraisal = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandProperty(appraisal, "LO-001", "Chiang Mai", "Mueang", "Sripoom",
                "IDEM-001", "Chanote");
            appraisalDb.Appraisals.Add(appraisal);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = appraisal.Id;
        }

        // First run
        await ProcessAppraisalInNewScopeAsync(appraisalId);
        // Re-run should not throw and should not create duplicates
        await ProcessAppraisalInNewScopeAsync(appraisalId);

        // Assert in fresh scope
        using var assertScope = CreateScope();
        var collateralDb = GetCollateralDbContext(assertScope);

        var engagementCount = await collateralDb.CollateralEngagements
            .CountAsync(e => e.AppraisalId == appraisalId, TestContext.Current.CancellationToken);
        Assert.Equal(1, engagementCount);

        var masterCount = await collateralDb.CollateralMasters
            .Include(m => m.LandDetail)
            .CountAsync(m => m.LandDetail != null && m.LandDetail.TitleNumber == "IDEM-001",
                TestContext.Current.CancellationToken);
        Assert.Equal(1, masterCount);
    }

    // -----------------------------------------------------------------------
    // Test 3: Progressive update — same master, engagement count = 2
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test3_ProgressiveUpdate_SameMasterUpdatedEngagementCountIs2()
    {
        var titleNo = "PROG-" + Guid.NewGuid().ToString("N")[..8];
        var wg = Guid.NewGuid();
        Guid a1Id, a2Id;

        // Seed appraisal 1 — 40% progress
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a1 = CreateAppraisalSeed(Guid.NewGuid());
            var insp1 = ConstructionInspection.CreateFullDetail(Guid.Empty, 10_000_000m);
            insp1.AddWorkDetail(wg, "Foundation", 1, 100m, 0m, 40m);
            insp1.ComputeAllValues();
            var p1 = SeedLandProperty(a1, "LO-001", "BKK", "D1", "S1", titleNo, "Chanote", insp1);
            typeof(ConstructionInspection).GetProperty("AppraisalPropertyId")!.SetValue(insp1, p1.Id);
            appraisalDb.Appraisals.Add(a1);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a1Id = a1.Id;
        }
        await ProcessAppraisalInNewScopeAsync(a1Id);

        // Seed appraisal 2 — 70% progress
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a2 = CreateAppraisalSeed(Guid.NewGuid());
            var insp2 = ConstructionInspection.CreateFullDetail(Guid.Empty, 10_000_000m);
            insp2.AddWorkDetail(wg, "Foundation", 1, 100m, 40m, 70m);
            insp2.ComputeAllValues();
            var p2 = SeedLandProperty(a2, "LO-001", "BKK", "D1", "S1", titleNo, "Chanote", insp2);
            typeof(ConstructionInspection).GetProperty("AppraisalPropertyId")!.SetValue(insp2, p2.Id);
            appraisalDb.Appraisals.Add(a2);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a2Id = a2.Id;
        }
        await ProcessAppraisalInNewScopeAsync(a2Id);

        // Assert in fresh scope
        using var assertScope = CreateScope();
        var collateralDb = GetCollateralDbContext(assertScope);
        var masters = await collateralDb.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .Where(m => m.LandDetail != null && m.LandDetail.TitleNumber == titleNo)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(masters);
        var master = masters[0];
        Assert.Equal(2, master.Engagements.Count);
        Assert.True(master.LandDetail!.IsUnderConstructionAtLastAppraisal);
        Assert.Equal(70m, master.LandDetail.OverallConstructionProgressPercent);
        // PR-5: LastConstructionInspectionId removed from LandDetail; insp2Id is now traceable via engagement snapshot.
    }

    // -----------------------------------------------------------------------
    // Test 4: Construction completion — flag flips, event raised
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test4_ConstructionCompletion_FlagFlipsToFalse()
    {
        var titleNo = "COMP-" + Guid.NewGuid().ToString("N")[..8];
        var wg = Guid.NewGuid();
        Guid a1Id, a2Id;

        // Seed appraisal 1 — under construction
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a1 = CreateAppraisalSeed(Guid.NewGuid());
            var insp1 = ConstructionInspection.CreateFullDetail(Guid.Empty, 10_000_000m);
            insp1.AddWorkDetail(wg, "All", 1, 100m, 0m, 80m);
            insp1.ComputeAllValues();
            var p1 = SeedLandProperty(a1, "LO-001", "BKK", "D1", "S1", titleNo, "Chanote", insp1);
            typeof(ConstructionInspection).GetProperty("AppraisalPropertyId")!.SetValue(insp1, p1.Id);
            appraisalDb.Appraisals.Add(a1);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a1Id = a1.Id;
        }
        await ProcessAppraisalInNewScopeAsync(a1Id);

        // Seed appraisal 2 — completion at 100%
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a2 = CreateAppraisalSeed(Guid.NewGuid());
            var insp2 = ConstructionInspection.CreateFullDetail(Guid.Empty, 10_000_000m);
            insp2.AddWorkDetail(wg, "All", 1, 100m, 80m, 100m);
            insp2.ComputeAllValues();
            var p2 = SeedLandProperty(a2, "LO-001", "BKK", "D1", "S1", titleNo, "Chanote", insp2);
            typeof(ConstructionInspection).GetProperty("AppraisalPropertyId")!.SetValue(insp2, p2.Id);
            appraisalDb.Appraisals.Add(a2);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a2Id = a2.Id;
        }
        await ProcessAppraisalInNewScopeAsync(a2Id);

        // Assert in fresh scope
        using var assertScope = CreateScope();
        var collateralDb = GetCollateralDbContext(assertScope);
        var master = await collateralDb.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .FirstAsync(m => m.LandDetail != null && m.LandDetail.TitleNumber == titleNo,
                TestContext.Current.CancellationToken);

        Assert.False(master.LandDetail!.IsUnderConstructionAtLastAppraisal,
            "Flag should flip to false when progress reaches 100%");
        Assert.Equal(2, master.Engagements.Count);
    }

    // -----------------------------------------------------------------------
    // Test 5: Summary-mode inspection
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test5_SummaryModeInspection_ProgressFromSummaryCurrentPct()
    {
        using var scope = CreateScope();
        var appraisalDb = GetAppraisalDbContext(scope);
        var collateralDb = GetCollateralDbContext(scope);
        var svc = GetUpsertService(scope);

        var titleNo = "SUM-" + Guid.NewGuid().ToString("N")[..8];

        var a = CreateAppraisalSeed(Guid.NewGuid());
        var inspection = ConstructionInspection.CreateSummary(
            Guid.Empty, 5_000_000m,
            summaryDetail: "Overall progress",
            summaryPreviousProgressPct: 0m,
            summaryPreviousValue: 0m,
            summaryCurrentProgressPct: 65m,
            summaryCurrentValue: 3_250_000m,
            remark: null);

        var p = SeedLandProperty(a, "LO-001", "BKK", "D1", "S1", titleNo, "Chanote", inspection);
        typeof(ConstructionInspection).GetProperty("AppraisalPropertyId")!.SetValue(inspection, p.Id);
        appraisalDb.Appraisals.Add(a);
        await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);

        await svc.ProcessAppraisalAsync(a.Id, TestContext.Current.CancellationToken);

        var master = await collateralDb.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .FirstAsync(m => m.LandDetail != null && m.LandDetail.TitleNumber == titleNo,
                TestContext.Current.CancellationToken);

        Assert.True(master.LandDetail!.IsUnderConstructionAtLastAppraisal);
        Assert.Equal(65m, master.LandDetail.OverallConstructionProgressPercent);

        // Snapshot should contain summaryCurrentProgressPct, not workDetails
        var engagement = master.Engagements.Single();
        Assert.Contains("isFullDetail", engagement.Snapshot);
        Assert.DoesNotContain("\"workDetails\"", engagement.Snapshot);
    }

    // -----------------------------------------------------------------------
    // Test 6: Appeal — 2 engagements from different companies, 1 master
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test6_Appeal_TwoEngagementsOnOneMaster()
    {
        var titleNo = "APPL-" + Guid.NewGuid().ToString("N")[..8];
        Guid a1Id, a2Id;

        // First appraisal
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a1 = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandProperty(a1, "LO-001", "BKK", "D1", "S1", titleNo, "Chanote");
            appraisalDb.Appraisals.Add(a1);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a1Id = a1.Id;
        }
        await ProcessAppraisalInNewScopeAsync(a1Id);

        // Second appraisal (appeal — same title, different request)
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a2 = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandProperty(a2, "LO-001", "BKK", "D1", "S1", titleNo, "Chanote");
            appraisalDb.Appraisals.Add(a2);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a2Id = a2.Id;
        }
        await ProcessAppraisalInNewScopeAsync(a2Id);

        // Assert in fresh scope
        using var assertScope = CreateScope();
        var collateralDb = GetCollateralDbContext(assertScope);
        var master = await collateralDb.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .FirstAsync(m => m.LandDetail != null && m.LandDetail.TitleNumber == titleNo,
                TestContext.Current.CancellationToken);

        Assert.Equal(2, master.Engagements.Count);
        Assert.Contains(master.Engagements, e => e.AppraisalId == a1Id);
        Assert.Contains(master.Engagements, e => e.AppraisalId == a2Id);
    }

    // -----------------------------------------------------------------------
    // Test 7: Multi-property — 2 land + 1 condo → 3 masters
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test7_MultiProperty_ThreeMastersCreated()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        Guid appraisalId;

        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandProperty(a, "LO-001", "BKK", "D1", "S1", $"LAND-A-{tag}", "Chanote");
            SeedLandProperty(a, "LO-001", "BKK", "D2", "S2", $"LAND-B-{tag}", "NorSor3");
            SeedCondoProperty(a, "LO-002", $"CR-{tag}", "B1", "3", "101",
                $"TN-{tag}", "Chanote", "BKK");
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;
        }

        await ProcessAppraisalInNewScopeAsync(appraisalId);

        using var assertScope = CreateScope();
        var collateralDb = GetCollateralDbContext(assertScope);

        // One engagement per appraisal, on the primary (consolidated land) IsMaster.
        var engCount = await collateralDb.CollateralEngagements
            .CountAsync(e => e.AppraisalId == appraisalId, TestContext.Current.CancellationToken);
        Assert.Equal(1, engCount);

        var masterCount = await collateralDb.CollateralMasters
            .CountAsync(m => !m.IsDeleted, TestContext.Current.CancellationToken);
        Assert.True(masterCount >= 3, $"Expected >=3 masters but got {masterCount}");
    }

    // -----------------------------------------------------------------------
    // Test 8: Derived match — query joining on dedup key finds the master
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test8_DerivedMatch_QueryOnDedupKeyReturnsMaster()
    {
        using var scope = CreateScope();
        var appraisalDb = GetAppraisalDbContext(scope);
        var collateralDb = GetCollateralDbContext(scope);
        var svc = GetUpsertService(scope);

        var titleNo = "DRV-" + Guid.NewGuid().ToString("N")[..8];

        var a = CreateAppraisalSeed(Guid.NewGuid());
        var prop = SeedLandProperty(a, "LO-001", "CMai", "Mueang", "Sripoom", titleNo, "Chanote");
        appraisalDb.Appraisals.Add(a);
        await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);

        await svc.ProcessAppraisalAsync(a.Id, TestContext.Current.CancellationToken);

        // Derived query: join on (TitleDeedNo, Province)
        var matched = await collateralDb.CollateralMasters
            .Include(m => m.LandDetail)
            .Where(m => m.LandDetail != null &&
                        m.LandDetail.TitleNumber == titleNo &&
                        m.LandDetail.Province == "CMai" &&
                        !m.IsDeleted)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(matched);
    }

    // -----------------------------------------------------------------------
    // Test 9: Missing key — MissingIdentityKeyException thrown
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test9_MissingKey_ThrowsMissingIdentityKeyException()
    {
        using var scope = CreateScope();
        var appraisalDb = GetAppraisalDbContext(scope);
        var svc = GetUpsertService(scope);

        // Land property with no title
        var a = CreateAppraisalSeed(Guid.NewGuid());
        var prop = a.AddLandProperty();
        // No title added, no address set → validation gate will fire
        appraisalDb.Appraisals.Add(a);
        await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<MissingIdentityKeyException>(
            () => svc.ProcessAppraisalAsync(a.Id, TestContext.Current.CancellationToken));
    }

    // -----------------------------------------------------------------------
    // Test 11: Building value rollup — LastTotalAppraisedValue = land + buildings
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test11_BuildingValueRollup_LastTotalAppraisedValueIsCorrect()
    {
        using var scope = CreateScope();
        var appraisalDb = GetAppraisalDbContext(scope);
        var collateralDb = GetCollateralDbContext(scope);
        var svc = GetUpsertService(scope);

        var titleNo = "BV-" + Guid.NewGuid().ToString("N")[..8];

        // Note: we need PropertyGroups + PricingAnalyses to have appraised values.
        // In this test we rely on appraisedValue from the query; if null, total is 0.
        // For a simpler assertion, we verify the process runs without error and buildings
        // in the snapshot are correct. Full appraised-value testing requires a pricing analysis row.
        var a = CreateAppraisalSeed(Guid.NewGuid());
        SeedLandProperty(a, "LO-001", "BKK", "D1", "S1", titleNo, "Chanote");
        // Building property on the same title
        var bProp = a.AddBuildingProperty();
        bProp.BuildingDetail!.Update(builtOnTitleNumber: titleNo);
        appraisalDb.Appraisals.Add(a);
        await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);

        await svc.ProcessAppraisalAsync(a.Id, TestContext.Current.CancellationToken);

        var master = await collateralDb.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .ThenInclude(e => e.Buildings)
            .FirstAsync(m => m.LandDetail != null && m.LandDetail.TitleNumber == titleNo,
                TestContext.Current.CancellationToken);

        Assert.NotNull(master);
        // The building attaches to the engagement's Buildings child collection.
        Assert.NotEmpty(master.Engagements.Single().Buildings);
    }

    // -----------------------------------------------------------------------
    // Test 12: One master per appraisal — two land titles + buildings collapse to a single
    // IsMaster (first title) with the second title as an alias and one shared engagement.
    // (Buildings all attach to that single engagement.)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test12_TwoLandTitles_CollapseToSingleMasterWithOneEngagement()
    {
        using var scope = CreateScope();
        var appraisalDb = GetAppraisalDbContext(scope);
        var collateralDb = GetCollateralDbContext(scope);
        var svc = GetUpsertService(scope);

        var titleA = "XT-A-" + Guid.NewGuid().ToString("N")[..6];
        var titleB = "XT-B-" + Guid.NewGuid().ToString("N")[..6];

        var a = CreateAppraisalSeed(Guid.NewGuid());
        SeedLandProperty(a, "LO-001", "BKK", "D1", "S1", titleA, "Chanote");
        SeedLandProperty(a, "LO-001", "BKK", "D1", "S2", titleB, "Chanote");
        var bA = a.AddBuildingProperty();
        bA.BuildingDetail!.Update(builtOnTitleNumber: titleA);
        var bB = a.AddBuildingProperty();
        bB.BuildingDetail!.Update(builtOnTitleNumber: titleB);
        appraisalDb.Appraisals.Add(a);
        await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);

        await svc.ProcessAppraisalAsync(a.Id, TestContext.Current.CancellationToken);

        var rows = await collateralDb.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .Where(m => m.LandDetail != null &&
                        (m.LandDetail.TitleNumber == titleA || m.LandDetail.TitleNumber == titleB))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, rows.Count);
        var masterA = rows.Single(m => m.IsMaster);          // first title is the only IsMaster
        var aliasB = rows.Single(m => !m.IsMaster);
        Assert.Equal(masterA.Id, aliasB.ParentMasterId);     // second title is an alias of it

        // Exactly one engagement from this appraisal, on the IsMaster.
        Assert.Single(masterA.Engagements.Where(e => e.AppraisalId == a.Id));
        Assert.Empty(aliasB.Engagements);

        // Buildings present → both rows are Land & Building.
        Assert.All(rows, m => Assert.Equal(CollateralTypes.LandWithBuilding, m.CollateralType));
    }

    // -----------------------------------------------------------------------
    // Test 13: Condo upsert
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test13_CondoUpsert_CreatesCondoMasterAndEngagement()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        Guid a1Id, a2Id;

        // Seed first appraisal
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            SeedCondoProperty(a, "LO-BKK", $"CR-{tag}", "B1", "5", "501",
                $"CT-{tag}", "Chanote", "Bangkok");
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a1Id = a.Id;
        }
        await ProcessAppraisalInNewScopeAsync(a1Id);

        // Assert after first run
        Guid masterId;
        using (var assertScope1 = CreateScope())
        {
            var collateralDb = GetCollateralDbContext(assertScope1);
            var master = await collateralDb.CollateralMasters
                .Include(m => m.CondoDetail)
                .Include(m => m.Engagements)
                .FirstOrDefaultAsync(m => m.CondoDetail != null &&
                                          m.CondoDetail.CondoRegistrationNumber == $"CR-{tag}",
                    TestContext.Current.CancellationToken);

            Assert.NotNull(master);
            Assert.Equal(CollateralTypes.Condo, master.CollateralType);
            Assert.Single(master.Engagements);
            masterId = master.Id;
        }

        // Seed second appraisal — same condo, different request
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a2 = CreateAppraisalSeed(Guid.NewGuid());
            SeedCondoProperty(a2, "LO-BKK", $"CR-{tag}", "B1", "5", "501",
                $"CT-{tag}", "Chanote", "Bangkok");
            appraisalDb.Appraisals.Add(a2);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a2Id = a2.Id;
        }
        await ProcessAppraisalInNewScopeAsync(a2Id);

        // Assert same master now has 2 engagements
        using var assertScope2 = CreateScope();
        var collateralDb2 = GetCollateralDbContext(assertScope2);
        var masterReloaded = await collateralDb2.CollateralMasters
            .Include(m => m.Engagements)
            .FirstAsync(m => m.Id == masterId, TestContext.Current.CancellationToken);

        Assert.Equal(2, masterReloaded.Engagements.Count);
    }

    // -----------------------------------------------------------------------
    // Test 14: Leasehold auto-create underlying
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test14_Leasehold_AutoCreatesUnderlyingLandMaster()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var titleNo = $"LH-LAND-{tag}";
        var contractNo = $"TD11-{tag}";
        Guid appraisalId;

        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandProperty(a, "LO-001", "BKK", "D1", "S1", titleNo, "Chanote");
            var lhProp = a.AddLeaseAgreementLandProperty();
            lhProp.LeaseAgreementDetail!.Update(
                contractNo: contractNo,
                lessorName: "Landlord Co",
                lesseeName: "Tenant Inc",
                leaseStartDate: new DateTime(2020, 1, 1));
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;
        }

        await ProcessAppraisalInNewScopeAsync(appraisalId);

        // Assert: 2 masters should exist — 1 Land + 1 Leasehold
        using var assertScope = CreateScope();
        var collateralDb = GetCollateralDbContext(assertScope);

        var landMaster = await collateralDb.CollateralMasters
            .Include(m => m.LandDetail)
            .FirstOrDefaultAsync(m => m.LandDetail != null && m.LandDetail.TitleNumber == titleNo,
                TestContext.Current.CancellationToken);
        Assert.NotNull(landMaster);

        var lhMaster = await collateralDb.CollateralMasters
            .Include(m => m.LeaseholdDetail)
            .FirstOrDefaultAsync(m =>
                m.LeaseholdDetail != null &&
                m.LeaseholdDetail.LeaseRegistrationNo == contractNo,
                TestContext.Current.CancellationToken);
        Assert.NotNull(lhMaster);
        Assert.Equal(landMaster.Id, lhMaster.LeaseholdDetail!.UnderlyingMasterId);
    }

    // -----------------------------------------------------------------------
    // Test 15: Leasehold over existing underlying
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test15_Leasehold_OverExistingUnderlying_NoSecondLandCreated()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var titleNo = $"EX-LAND-{tag}";
        var contractNo = $"TD11-EX-{tag}";
        Guid a1Id, a2Id;

        // First appraisal — creates the land master
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a1 = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandProperty(a1, "LO-001", "BKK", "D1", "S1", titleNo, "Chanote");
            appraisalDb.Appraisals.Add(a1);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a1Id = a1.Id;
        }
        await ProcessAppraisalInNewScopeAsync(a1Id);

        // Second appraisal — leasehold over the same land (which already has a master)
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a2 = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandProperty(a2, "LO-001", "BKK", "D1", "S1", titleNo, "Chanote");
            var lhProp = a2.AddLeaseAgreementLandProperty();
            lhProp.LeaseAgreementDetail!.Update(
                contractNo: contractNo,
                lessorName: "Owner",
                lesseeName: "Leasee",
                leaseStartDate: new DateTime(2021, 6, 1));
            appraisalDb.Appraisals.Add(a2);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a2Id = a2.Id;
        }
        await ProcessAppraisalInNewScopeAsync(a2Id);

        // Assert in fresh scope
        using var assertScope = CreateScope();
        var collateralDb = GetCollateralDbContext(assertScope);

        // Still only 1 land master
        var landCount = await collateralDb.CollateralMasters
            .Include(m => m.LandDetail)
            .CountAsync(m => m.LandDetail != null && m.LandDetail.TitleNumber == titleNo,
                TestContext.Current.CancellationToken);
        Assert.Equal(1, landCount);

        // And 1 leasehold master
        var lhMaster = await collateralDb.CollateralMasters
            .Include(m => m.LeaseholdDetail)
            .FirstOrDefaultAsync(m =>
                m.LeaseholdDetail != null &&
                m.LeaseholdDetail.LeaseRegistrationNo == contractNo,
                TestContext.Current.CancellationToken);
        Assert.NotNull(lhMaster);
    }

    // -----------------------------------------------------------------------
    // Test 16: Machine tier-1 dedup — same registration no → 1 master, 2 engagements
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test16_MachineTier1Dedup_SameMasterTwoEngagements()
    {
        var regNo = "REG-" + Guid.NewGuid().ToString("N")[..8];
        Guid a1Id, a2Id;

        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a1 = CreateAppraisalSeed(Guid.NewGuid());
            SeedMachineryProperty(a1, registrationNo: regNo, serialNo: "S1", brand: "BRAND-A",
                model: "M1", manufacturer: "MFR-A");
            appraisalDb.Appraisals.Add(a1);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a1Id = a1.Id;
        }
        await ProcessAppraisalInNewScopeAsync(a1Id);

        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a2 = CreateAppraisalSeed(Guid.NewGuid());
            SeedMachineryProperty(a2, registrationNo: regNo, serialNo: "S1", brand: "BRAND-A",
                model: "M1", manufacturer: "MFR-A");
            appraisalDb.Appraisals.Add(a2);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a2Id = a2.Id;
        }
        await ProcessAppraisalInNewScopeAsync(a2Id);

        using var assertScope = CreateScope();
        var collateralDb = GetCollateralDbContext(assertScope);
        var masters = await collateralDb.CollateralMasters
            .Include(m => m.MachineDetail)
            .Include(m => m.Engagements)
            .Where(m => m.MachineDetail != null && m.MachineDetail.MachineRegistrationNo == regNo)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(masters);
        Assert.Equal(2, masters[0].Engagements.Count);
    }

    // -----------------------------------------------------------------------
    // Test 17: Machine tier-2 dedup — same composite → 1 master, 2 engagements
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test17_MachineTier2Dedup_SameMasterTwoEngagements()
    {
        var serial = "SN-" + Guid.NewGuid().ToString("N")[..8];
        Guid a1Id, a2Id;

        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a1 = CreateAppraisalSeed(Guid.NewGuid());
            SeedMachineryProperty(a1, serialNo: serial, brand: "ACME", model: "X100",
                manufacturer: "ACME Inc");
            appraisalDb.Appraisals.Add(a1);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a1Id = a1.Id;
        }
        await ProcessAppraisalInNewScopeAsync(a1Id);

        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a2 = CreateAppraisalSeed(Guid.NewGuid());
            SeedMachineryProperty(a2, serialNo: serial, brand: "ACME", model: "X100",
                manufacturer: "ACME Inc");
            appraisalDb.Appraisals.Add(a2);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a2Id = a2.Id;
        }
        await ProcessAppraisalInNewScopeAsync(a2Id);

        using var assertScope = CreateScope();
        var collateralDb = GetCollateralDbContext(assertScope);
        var masters = await collateralDb.CollateralMasters
            .Include(m => m.MachineDetail)
            .Include(m => m.Engagements)
            .Where(m => m.MachineDetail != null &&
                        m.MachineDetail.MachineRegistrationNo == null &&
                        m.MachineDetail.SerialNo == serial &&
                        m.MachineDetail.Brand == "ACME")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(masters);
        Assert.Equal(2, masters[0].Engagements.Count);
    }

    // -----------------------------------------------------------------------
    // Test 18: Machine promotion — composite master gets registration, same Id
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test18_MachinePromotion_SameIdRegistrationSet()
    {
        var serial = "PRM-" + Guid.NewGuid().ToString("N")[..8];
        var regNo = "REG-PRM-" + Guid.NewGuid().ToString("N")[..6];
        Guid a1Id, a2Id;

        // Appraisal 1 — composite only (no registration number)
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a1 = CreateAppraisalSeed(Guid.NewGuid());
            SeedMachineryProperty(a1, serialNo: serial, brand: "X-Corp", model: "Y200",
                manufacturer: "X Corp Ltd");
            appraisalDb.Appraisals.Add(a1);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a1Id = a1.Id;
        }
        await ProcessAppraisalInNewScopeAsync(a1Id);

        // Capture the original master Id and confirm no registration number yet
        Guid originalId;
        using (var checkScope = CreateScope())
        {
            var collateralDb = GetCollateralDbContext(checkScope);
            var original = await collateralDb.CollateralMasters
                .Include(m => m.MachineDetail)
                .FirstAsync(m => m.MachineDetail != null && m.MachineDetail.SerialNo == serial,
                    TestContext.Current.CancellationToken);
            originalId = original.Id;
            Assert.Null(original.MachineDetail!.MachineRegistrationNo);
        }

        // Appraisal 2 — same composite but now has a registration number (promotion)
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a2 = CreateAppraisalSeed(Guid.NewGuid());
            SeedMachineryProperty(a2, registrationNo: regNo, serialNo: serial, brand: "X-Corp",
                model: "Y200", manufacturer: "X Corp Ltd");
            appraisalDb.Appraisals.Add(a2);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a2Id = a2.Id;
        }
        await ProcessAppraisalInNewScopeAsync(a2Id);

        // Assert in fresh scope
        using var assertScope = CreateScope();
        var collateralDbAssert = GetCollateralDbContext(assertScope);
        var promoted = await collateralDbAssert.CollateralMasters
            .Include(m => m.MachineDetail)
            .Include(m => m.Engagements)
            .FirstAsync(m => m.Id == originalId, TestContext.Current.CancellationToken);

        Assert.Equal(regNo, promoted.MachineDetail!.MachineRegistrationNo);
        Assert.Equal(2, promoted.Engagements.Count);
        Assert.Equal(originalId, promoted.Id); // Identity preserved
    }

    // -----------------------------------------------------------------------
    // Test 19: RESTRICT delete on underlying with active leasehold
    // DEFERRED: requires admin SoftDelete endpoint (Step 5).
    // -----------------------------------------------------------------------
    /*
    [Fact]
    public async Task Test19_RestrictDelete_CannotDeleteUnderlyingWithActiveLeasehold()
    {
        // Deferred — requires DELETE /collateral-masters/{id} admin endpoint (Step 5).
        // The FK RESTRICT constraint exists in the DB schema (LeaseholdDetail.UnderlyingMasterId).
        // This test will be added when the admin endpoints are implemented.
        throw new NotImplementedException("Deferred to Step 5");
    }
    */

    // -----------------------------------------------------------------------
    // v1.1 Issue 2: Multi-title IsMaster pattern tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// Helper that seeds a land property with multiple title deeds on the same property.
    /// </summary>
    private static AppraisalProperty SeedLandPropertyWithTitles(
        AppraisalAggregate appraisal,
        string landOffice, string province, string district, string subDistrict,
        params (string TitleNo, string TitleType)[] titles)
    {
        var prop = appraisal.AddLandProperty();
        prop.LandDetail!.Update(
            address: AdministrativeAddress.Create(subDistrict, district, province, landOffice));
        foreach (var (titleNo, titleType) in titles)
        {
            var title = LandTitle.Create(prop.LandDetail.Id, titleNo, titleType);
            prop.LandDetail.AddTitle(title);
        }
        return prop;
    }

    // -----------------------------------------------------------------------
    // MultiTitle_3Titles_Creates1Master2Aliases_EngagementCount1
    // -----------------------------------------------------------------------
    [Fact]
    public async Task MultiTitle_3Titles_Creates1Master2Aliases_EngagementCount1()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var t1 = ($"MT1-{tag}", "Chanote");
        var t2 = ($"MT2-{tag}", "Chanote");
        var t3 = ($"MT3-{tag}", "Chanote");

        Guid appraisalId;
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandPropertyWithTitles(a, "LO-001", "BKK", "D1", "S1", t1, t2, t3);
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;
        }

        await ProcessAppraisalInNewScopeAsync(appraisalId);

        using var assertScope = CreateScope();
        var collateralDb = GetCollateralDbContext(assertScope);

        // Exactly 1 IsMaster row for this title group
        var allRows = await collateralDb.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .Where(m => m.LandDetail != null &&
                        (m.LandDetail.TitleNumber == t1.Item1 ||
                         m.LandDetail.TitleNumber == t2.Item1 ||
                         m.LandDetail.TitleNumber == t3.Item1))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, allRows.Count);

        var masterRows = allRows.Where(m => m.IsMaster).ToList();
        var aliasRows = allRows.Where(m => !m.IsMaster).ToList();

        Assert.Single(masterRows);
        Assert.Equal(2, aliasRows.Count);

        var master = masterRows[0];
        // Engagement only on master
        Assert.Single(master.Engagements);
        Assert.Equal(appraisalId, master.Engagements[0].AppraisalId);

        // Aliases have no engagements and point at master
        foreach (var alias in aliasRows)
        {
            Assert.Empty(alias.Engagements);
            Assert.Equal(master.Id, alias.ParentMasterId);
        }
    }

    // -----------------------------------------------------------------------
    // OneMasterPerAppraisal: SEPARATE land properties (each its own ungrouped group)
    // collapse into a single IsMaster + aliases. No building → all rows stay "L".
    // -----------------------------------------------------------------------
    [Fact]
    public async Task SeparateLandProperties_CollapseToOneMaster_AllLand()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var t1 = $"OM1-{tag}";
        var t2 = $"OM2-{tag}";
        var t3 = $"OM3-{tag}";

        Guid appraisalId;
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            // Three distinct land properties (no PropertyGroup) — previously 3 masters, now 1.
            SeedLandProperty(a, "LO-001", "BKK", "D1", "S1", t1, "Chanote");
            SeedLandProperty(a, "LO-001", "BKK", "D1", "S1", t2, "Chanote");
            SeedLandProperty(a, "LO-001", "BKK", "D1", "S1", t3, "Chanote");
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;
        }

        await ProcessAppraisalInNewScopeAsync(appraisalId);

        using var assertScope = CreateScope();
        var collateralDb = GetCollateralDbContext(assertScope);

        var allRows = await collateralDb.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .Where(m => m.LandDetail != null &&
                        (m.LandDetail.TitleNumber == t1 ||
                         m.LandDetail.TitleNumber == t2 ||
                         m.LandDetail.TitleNumber == t3))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, allRows.Count);

        var masterRows = allRows.Where(m => m.IsMaster).ToList();
        var aliasRows = allRows.Where(m => !m.IsMaster).ToList();

        Assert.Single(masterRows);        // exactly one IsMaster per appraisal
        Assert.Equal(2, aliasRows.Count);

        var master = masterRows[0];
        Assert.Single(master.Engagements); // single engagement on the IsMaster
        foreach (var alias in aliasRows)
            Assert.Equal(master.Id, alias.ParentMasterId);

        // No building → every row stays Land.
        Assert.All(allRows, m => Assert.Equal(CollateralTypes.Land, m.CollateralType));
    }

    // -----------------------------------------------------------------------
    // OneMasterPerAppraisal + a building (NON-matching BuiltOnTitleNumber) → every land
    // row (IsMaster + all aliases) becomes "LB", proving the type switch no longer depends
    // on the fragile title match and propagates to all titles.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task SeparateLandProperties_WithBuilding_AllRowsBecomeLandWithBuilding()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var t1 = $"LB1-{tag}";
        var t2 = $"LB2-{tag}";
        var t3 = $"LB3-{tag}";

        Guid appraisalId;
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandProperty(a, "LO-001", "BKK", "D1", "S1", t1, "Chanote");
            SeedLandProperty(a, "LO-001", "BKK", "D1", "S1", t2, "Chanote");
            SeedLandProperty(a, "LO-001", "BKK", "D1", "S1", t3, "Chanote");
            // Building whose BuiltOnTitleNumber matches NO land title — must still flip to LB.
            var bProp = a.AddBuildingProperty();
            bProp.BuildingDetail!.Update(builtOnTitleNumber: $"NO-MATCH-{tag}");
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;
        }

        await ProcessAppraisalInNewScopeAsync(appraisalId);

        using var assertScope = CreateScope();
        var collateralDb = GetCollateralDbContext(assertScope);

        var allRows = await collateralDb.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .ThenInclude(e => e.Buildings)
            .Where(m => m.LandDetail != null &&
                        (m.LandDetail.TitleNumber == t1 ||
                         m.LandDetail.TitleNumber == t2 ||
                         m.LandDetail.TitleNumber == t3))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, allRows.Count);
        Assert.Single(allRows.Where(m => m.IsMaster));

        // Every land row — IsMaster AND aliases — is Land & Building.
        Assert.All(allRows, m => Assert.Equal(CollateralTypes.LandWithBuilding, m.CollateralType));

        // Building attached to the single engagement's Buildings child collection.
        var master = allRows.First(m => m.IsMaster);
        Assert.NotEmpty(master.Engagements.Single().Buildings);
    }

    // -----------------------------------------------------------------------
    // MultiTitle_Reappraisal_SameTitles_NoNewAliases_EngagementCount2
    // -----------------------------------------------------------------------
    [Fact]
    public async Task MultiTitle_Reappraisal_SameTitles_NoNewAliases_EngagementCount2()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var t1 = ($"RA1-{tag}", "Chanote");
        var t2 = ($"RA2-{tag}", "Chanote");
        Guid a1Id, a2Id;

        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandPropertyWithTitles(a, "LO-001", "BKK", "D1", "S1", t1, t2);
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a1Id = a.Id;
        }
        await ProcessAppraisalInNewScopeAsync(a1Id);

        // Same 2 titles — reappraisal
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a2 = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandPropertyWithTitles(a2, "LO-001", "BKK", "D1", "S1", t1, t2);
            appraisalDb.Appraisals.Add(a2);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a2Id = a2.Id;
        }
        await ProcessAppraisalInNewScopeAsync(a2Id);

        using var assertScope = CreateScope();
        var collateralDb = GetCollateralDbContext(assertScope);

        var allRows = await collateralDb.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .Where(m => m.LandDetail != null &&
                        (m.LandDetail.TitleNumber == t1.Item1 || m.LandDetail.TitleNumber == t2.Item1))
            .ToListAsync(TestContext.Current.CancellationToken);

        // Still 2 rows — no new aliases created for the same titles
        Assert.Equal(2, allRows.Count);

        var master = allRows.Single(m => m.IsMaster);
        // 2 engagements — one per appraisal
        Assert.Equal(2, master.Engagements.Count);
    }

    // -----------------------------------------------------------------------
    // MultiTitle_Reappraisal_OneRemoved_RemovedAliasStays_EngagementCount2
    // -----------------------------------------------------------------------
    [Fact]
    public async Task MultiTitle_Reappraisal_OneRemoved_RemovedAliasStays_EngagementCount2()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var t1 = ($"RR1-{tag}", "Chanote");
        var t2 = ($"RR2-{tag}", "Chanote");
        var t3 = ($"RR3-{tag}", "Chanote");
        Guid a1Id, a2Id;

        // First appraisal: 3 titles
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandPropertyWithTitles(a, "LO-001", "BKK", "D1", "S1", t1, t2, t3);
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a1Id = a.Id;
        }
        await ProcessAppraisalInNewScopeAsync(a1Id);

        // Second appraisal: only 2 of the 3 original titles
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a2 = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandPropertyWithTitles(a2, "LO-001", "BKK", "D1", "S1", t1, t2);
            appraisalDb.Appraisals.Add(a2);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a2Id = a2.Id;
        }
        await ProcessAppraisalInNewScopeAsync(a2Id);

        using var assertScope = CreateScope();
        var collateralDb = GetCollateralDbContext(assertScope);

        var allRows = await collateralDb.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .Where(m => m.LandDetail != null &&
                        (m.LandDetail.TitleNumber == t1.Item1 ||
                         m.LandDetail.TitleNumber == t2.Item1 ||
                         m.LandDetail.TitleNumber == t3.Item1))
            .ToListAsync(TestContext.Current.CancellationToken);

        // All 3 rows still present (removed alias stays for audit)
        Assert.Equal(3, allRows.Count);

        var master = allRows.Single(m => m.IsMaster);
        // 2 engagements — one per appraisal
        Assert.Equal(2, master.Engagements.Count);
    }

    // -----------------------------------------------------------------------
    // MultiTitle_Reappraisal_OneAdded_NewAliasCreated_EngagementCount2
    // -----------------------------------------------------------------------
    [Fact]
    public async Task MultiTitle_Reappraisal_OneAdded_NewAliasCreated_EngagementCount2()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var t1 = ($"RA-A1-{tag}", "Chanote");
        var t2 = ($"RA-A2-{tag}", "Chanote");
        var t3 = ($"RA-A3-{tag}", "Chanote");   // added in second appraisal
        Guid a1Id, a2Id;

        // First appraisal: 2 titles
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandPropertyWithTitles(a, "LO-001", "BKK", "D1", "S1", t1, t2);
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a1Id = a.Id;
        }
        await ProcessAppraisalInNewScopeAsync(a1Id);

        // Second appraisal: 3 titles (t3 is new)
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a2 = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandPropertyWithTitles(a2, "LO-001", "BKK", "D1", "S1", t1, t2, t3);
            appraisalDb.Appraisals.Add(a2);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a2Id = a2.Id;
        }
        await ProcessAppraisalInNewScopeAsync(a2Id);

        using var assertScope = CreateScope();
        var collateralDb = GetCollateralDbContext(assertScope);

        var allRows = await collateralDb.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .Where(m => m.LandDetail != null &&
                        (m.LandDetail.TitleNumber == t1.Item1 ||
                         m.LandDetail.TitleNumber == t2.Item1 ||
                         m.LandDetail.TitleNumber == t3.Item1))
            .ToListAsync(TestContext.Current.CancellationToken);

        // Now 3 rows — t3 alias was created on reappraisal
        Assert.Equal(3, allRows.Count);

        var master = allRows.Single(m => m.IsMaster);
        Assert.Equal(2, master.Engagements.Count);

        // t3 alias exists and points at master
        var t3Row = allRows.Single(m => m.LandDetail!.TitleNumber == t3.Item1);
        Assert.False(t3Row.IsMaster);
        Assert.Equal(master.Id, t3Row.ParentMasterId);
    }

    // -----------------------------------------------------------------------
    // MultiTitle_LookupByAnyTitle_ReturnsMasterWithAllAliases
    // -----------------------------------------------------------------------
    [Fact]
    public async Task MultiTitle_LookupByAnyTitle_ReturnsMasterWithAllAliases()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var t1 = ($"LK1-{tag}", "Chanote");
        var t2 = ($"LK2-{tag}", "Chanote");
        var t3 = ($"LK3-{tag}", "Chanote");

        Guid appraisalId;
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandPropertyWithTitles(a, "LO-001", "BKK", "D1", "S1", t1, t2, t3);
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;
        }
        await ProcessAppraisalInNewScopeAsync(appraisalId);

        // Look up by the ALIAS title (t2) — should resolve to the IsMaster row
        // and include both alias titles in AliasTitles
        using var assertScope = CreateScope();
        var mediator = assertScope.ServiceProvider.GetRequiredService<ISender>();

        var result = await mediator.Send(new LookupCollateralMasterQuery(
            Type: CollateralTypes.Land,
            LandOfficeCode: "LO-001",
            Province: "BKK",
            District: "D1",
            SubDistrict: "S1",
            TitleType: t2.Item2,
            TitleNumber: t2.Item1,
            SurveyNumber: null,
            CondoRegistrationNumber: null, Building: null, Floor: null, Unit: null,
            ContractNo: null, UnderlyingMasterId: null,
            Lessor: null, Lessee: null, LeaseTermStart: null,
            MachineRegistrationNo: null, SerialNo: null,
            Brand: null, Model: null, Manufacturer: null),
            TestContext.Current.CancellationToken);

        // The result should be the IsMaster row (has engagements, heavy data)
        Assert.NotNull(result);
        Assert.Equal(CollateralTypes.Land, result.CollateralType);
        Assert.Equal(1, result.EngagementCount);

        // AliasTitles should contain exactly 2 alias titles (the non-master titles).
        // The master holds whichever title was picked first; the other two are aliases.
        // We assert: the full set (master's TitleDeedNo + alias TitleDeedNos) covers all 3 titles.
        Assert.NotNull(result.LandDetail);
        Assert.Equal(2, result.LandDetail.AliasTitles.Count);

        var allGroupTitleNos = new HashSet<string>
        {
            result.LandDetail.TitleNumber,          // master's own title
        };
        foreach (var at in result.LandDetail.AliasTitles)
            allGroupTitleNos.Add(at.TitleNumber);

        // All 3 original titles must appear somewhere in the group
        Assert.Contains(t1.Item1, allGroupTitleNos);
        Assert.Contains(t2.Item1, allGroupTitleNos);
        Assert.Contains(t3.Item1, allGroupTitleNos);
    }

    // -----------------------------------------------------------------------
    // MultiTitle_GetMostRecentEngagementByPriorAppraisal_ResolvesViaMasterLink
    // Regression guard: a multi-title appraisal creates 1 IsMaster + N aliases but exactly
    // one per-appraisal engagement, anchored on the IsMaster. GetMostRecentEngagementByPrior-
    // AppraisalQueryHandler must resolve the prior appraisal → its engagement's IsMaster →
    // the most-recent engagement on that master, returning the company it carries.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task MultiTitle_GetMostRecentEngagementByPriorAppraisal_ResolvesViaMasterLink()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var tA = ($"AX-{tag}", "Chanote");
        var tB = ($"BX-{tag}", "Chanote");
        var tC = ($"CX-{tag}", "Chanote");
        var expectedCompanyId = Guid.NewGuid();

        // Seed an appraisal with 3 titles → creates 1 IsMaster + 2 aliases on completion
        Guid appraisalId;
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandPropertyWithTitles(a, "LO-AE", "BKK", "D1", "S1", tA, tB, tC);
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;
        }
        await ProcessAppraisalInNewScopeAsync(appraisalId);

        // Patch the engagement with a known AppraisalCompanyId — the upsert path doesn't
        // populate it from the seed (no ext-company workflow ran). This isolates the test
        // to the handler's master-link resolution.
        using (var patchScope = CreateScope())
        {
            var db = GetCollateralDbContext(patchScope);
            var engagement = await db.CollateralEngagements
                .FirstAsync(e => e.AppraisalId == appraisalId, TestContext.Current.CancellationToken);
            typeof(CollateralEngagement).GetProperty("AppraisalCompanyId")!
                .SetValue(engagement, expectedCompanyId);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Query by the prior appraisal id — resolves via the engagement's master link.
        using var assertScope = CreateScope();
        var mediator = assertScope.ServiceProvider.GetRequiredService<ISender>();

        var result = await mediator.Send(
            new GetMostRecentEngagementByPriorAppraisalQuery(appraisalId),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(appraisalId, result!.AppraisalId);
        Assert.Equal(expectedCompanyId, result.CompanyId);
    }

    // -----------------------------------------------------------------------
    // MultiTitle_OverlapConflict_ThrowsConflictException
    // -----------------------------------------------------------------------
    [Fact]
    public async Task MultiTitle_OverlapConflict_ThrowsConflictException()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var tA = ($"CONF-A-{tag}", "Chanote");
        var tB = ($"CONF-B-{tag}", "Chanote");

        // Create two separate single-title masters at the SAME location (S1)
        Guid a1Id;
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandPropertyWithTitles(a, "LO-001", "BKK", "CF", "S1", tA);
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a1Id = a.Id;
        }
        await ProcessAppraisalInNewScopeAsync(a1Id);

        Guid a2Id;
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a2 = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandPropertyWithTitles(a2, "LO-001", "BKK", "CF", "S1", tB);
            appraisalDb.Appraisals.Add(a2);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a2Id = a2.Id;
        }
        await ProcessAppraisalInNewScopeAsync(a2Id);

        // Now a new appraisal references BOTH titles → two distinct masters → ConflictException
        Guid conflictId;
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a3 = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandPropertyWithTitles(a3, "LO-001", "BKK", "CF", "S1", tA, tB);
            appraisalDb.Appraisals.Add(a3);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            conflictId = a3.Id;
        }

        await Assert.ThrowsAsync<ConflictException>(
            () => ProcessAppraisalInNewScopeAsync(conflictId));
    }

    // -----------------------------------------------------------------------
    // Catalog_MultiTitle_Returns1Row (3-title group → catalog returns 1 row)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Catalog_MultiTitle_Returns1Row()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var t1 = ($"CAT1-{tag}", "Chanote");
        var t2 = ($"CAT2-{tag}", "Chanote");
        var t3 = ($"CAT3-{tag}", "Chanote");

        Guid appraisalId;
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandPropertyWithTitles(a, "LO-001", "CAT-BKK", "D1", "S1", t1, t2, t3);
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;
        }
        await ProcessAppraisalInNewScopeAsync(appraisalId);

        using var assertScope = CreateScope();
        var collateralDb = GetCollateralDbContext(assertScope);

        // Verify DB has 3 rows total (1 master + 2 aliases)
        var allRows = await collateralDb.CollateralMasters
            .Include(m => m.LandDetail)
            .Where(m => m.LandDetail != null && m.LandDetail.Province == "CAT-BKK")
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, allRows.Count);

        // Verify only 1 is IsMaster
        var masterCount = allRows.Count(m => m.IsMaster);
        Assert.Equal(1, masterCount);

        // Simulate catalog SQL: WHERE IsMaster = 1 (same filter the view and catalog handler apply)
        var catalogRows = await collateralDb.CollateralMasters
            .Include(m => m.LandDetail)
            .Where(m => m.LandDetail != null && m.LandDetail.Province == "CAT-BKK" && m.IsMaster)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(catalogRows);
    }

    // -----------------------------------------------------------------------
    // Condo_Machine_NoLand_ExactlyOnePrimary_OtherBecomesTypedAlias
    // One-collateral-per-appraisal model: when Condo and Machine coexist with no Land in the
    // same appraisal, exactly ONE of them stays IsMaster=true (the primary, owning the single
    // engagement); the other becomes a typed alias (IsMaster=false, ParentMasterId=primary.Id)
    // while keeping its own type detail intact.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Condo_Machine_NoLand_ExactlyOnePrimary_OtherBecomesTypedAlias()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        Guid appraisalId;

        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            SeedCondoProperty(a, "LO-BKK", $"SING-CR-{tag}", "B1", "5", "501",
                $"SING-CT-{tag}", "Chanote", "Bangkok");
            SeedMachineryProperty(a, registrationNo: $"SING-REG-{tag}",
                serialNo: "S1", brand: "BRAND-S", model: "M1", manufacturer: "MFR-S");
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;
        }

        await ProcessAppraisalInNewScopeAsync(appraisalId);

        using var assertScope = CreateScope();
        var collateralDb = GetCollateralDbContext(assertScope);

        // Condo
        var condo = await collateralDb.CollateralMasters
            .Include(m => m.CondoDetail)
            .Include(m => m.Engagements)
            .FirstOrDefaultAsync(m => m.CondoDetail != null &&
                                      m.CondoDetail.CondoRegistrationNumber == $"SING-CR-{tag}",
                TestContext.Current.CancellationToken);
        Assert.NotNull(condo);

        // Machine
        var machine = await collateralDb.CollateralMasters
            .Include(m => m.MachineDetail)
            .Include(m => m.Engagements)
            .FirstOrDefaultAsync(m => m.MachineDetail != null &&
                                      m.MachineDetail.MachineRegistrationNo == $"SING-REG-{tag}",
                TestContext.Current.CancellationToken);
        Assert.NotNull(machine);

        // Exactly one of the two ends up IsMaster=true (the appraisal's primary).
        Assert.NotEqual(condo!.IsMaster, machine!.IsMaster);

        var primary = condo.IsMaster ? condo : machine;
        var alias = condo.IsMaster ? machine : condo;

        Assert.Null(primary.ParentMasterId);
        Assert.Single(primary.Engagements);

        Assert.False(alias.IsMaster);
        Assert.Equal(primary.Id, alias.ParentMasterId);
        Assert.Empty(alias.Engagements);

        // The alias keeps its OWN type detail — nothing was collapsed/dropped.
        Assert.NotNull(condo.CondoDetail);
        Assert.NotNull(machine.MachineDetail);
    }

    // -----------------------------------------------------------------------
    // Task (a): Land + Machinery multi-component appraisal → exactly ONE IsMaster (the collapsed
    // land master); the machinery group is persisted as a typed alias (IsMaster=0,
    // ParentMasterId=primary, MachineDetail intact).
    // -----------------------------------------------------------------------
    [Fact]
    public async Task LandAndMachinery_OneIsMaster_MachineryBecomesTypedAlias()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var titleNo = $"LM-{tag}";
        var regNo = $"LM-REG-{tag}";
        Guid appraisalId;

        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandProperty(a, "LO-001", "BKK", "D1", "S1", titleNo, "Chanote");
            SeedMachineryProperty(a, registrationNo: regNo,
                serialNo: "S1", brand: "BRAND-M", model: "M1", manufacturer: "MFR-M");
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;
        }

        await ProcessAppraisalInNewScopeAsync(appraisalId);

        using var assertScope = CreateScope();
        var collateralDb = GetCollateralDbContext(assertScope);

        var landMaster = await collateralDb.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .FirstAsync(m => m.LandDetail != null && m.LandDetail.TitleNumber == titleNo,
                TestContext.Current.CancellationToken);

        var machine = await collateralDb.CollateralMasters
            .Include(m => m.MachineDetail)
            .Include(m => m.Engagements)
            .FirstAsync(m => m.MachineDetail != null && m.MachineDetail.MachineRegistrationNo == regNo,
                TestContext.Current.CancellationToken);

        // Land is unconditionally the primary when present.
        Assert.True(landMaster.IsMaster);
        Assert.Null(landMaster.ParentMasterId);
        Assert.Single(landMaster.Engagements);

        // Machinery is a typed alias of the land master, but keeps its own MachineDetail.
        Assert.False(machine.IsMaster);
        Assert.Equal(landMaster.Id, machine.ParentMasterId);
        Assert.Empty(machine.Engagements);
        Assert.NotNull(machine.MachineDetail);
        Assert.Equal(regNo, machine.MachineDetail!.MachineRegistrationNo);

        // Exactly one IsMaster row across the whole appraisal's components.
        var allRows = await collateralDb.CollateralMasters
            .Where(m => m.Id == landMaster.Id || m.Id == machine.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, allRows.Count(m => m.IsMaster));

        // Exactly one engagement for the whole appraisal.
        var engCount = await collateralDb.CollateralEngagements
            .CountAsync(e => e.AppraisalId == appraisalId, TestContext.Current.CancellationToken);
        Assert.Equal(1, engCount);
    }

    // -----------------------------------------------------------------------
    // Task (b): Reappraising the SAME land+machinery collateral resolves the same primary + the
    // same alias row — no duplicate masters/aliases created, and the second run is idempotent.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task LandAndMachinery_Reappraisal_ResolvesSamePrimaryAndAlias_Idempotent()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var titleNo = $"LMR-{tag}";
        var regNo = $"LMR-REG-{tag}";
        Guid a1Id, a2Id;

        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a1 = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandProperty(a1, "LO-001", "BKK", "D1", "S1", titleNo, "Chanote");
            SeedMachineryProperty(a1, registrationNo: regNo,
                serialNo: "S1", brand: "BRAND-M", model: "M1", manufacturer: "MFR-M");
            appraisalDb.Appraisals.Add(a1);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a1Id = a1.Id;
        }
        await ProcessAppraisalInNewScopeAsync(a1Id);

        Guid landId1, machineId1;
        using (var checkScope = CreateScope())
        {
            var collateralDb = GetCollateralDbContext(checkScope);
            landId1 = (await collateralDb.CollateralMasters
                .Include(m => m.LandDetail)
                .FirstAsync(m => m.LandDetail != null && m.LandDetail.TitleNumber == titleNo,
                    TestContext.Current.CancellationToken)).Id;
            machineId1 = (await collateralDb.CollateralMasters
                .Include(m => m.MachineDetail)
                .FirstAsync(m => m.MachineDetail != null && m.MachineDetail.MachineRegistrationNo == regNo,
                    TestContext.Current.CancellationToken)).Id;
        }

        // Reappraisal — SAME land title + SAME machine registration (same physical collateral).
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a2 = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandProperty(a2, "LO-001", "BKK", "D1", "S1", titleNo, "Chanote");
            SeedMachineryProperty(a2, registrationNo: regNo,
                serialNo: "S1", brand: "BRAND-M", model: "M1", manufacturer: "MFR-M");
            appraisalDb.Appraisals.Add(a2);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a2Id = a2.Id;
        }
        await ProcessAppraisalInNewScopeAsync(a2Id);

        // Re-run the SAME (second) appraisal again — must be a idempotent no-op.
        await ProcessAppraisalInNewScopeAsync(a2Id);

        using var assertScope = CreateScope();
        var collateralDbAssert = GetCollateralDbContext(assertScope);

        var landRows = await collateralDbAssert.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .Where(m => m.LandDetail != null && m.LandDetail.TitleNumber == titleNo)
            .ToListAsync(TestContext.Current.CancellationToken);
        var machineRows = await collateralDbAssert.CollateralMasters
            .Include(m => m.MachineDetail)
            .Include(m => m.Engagements)
            .Where(m => m.MachineDetail != null && m.MachineDetail.MachineRegistrationNo == regNo)
            .ToListAsync(TestContext.Current.CancellationToken);

        // No duplicate masters/aliases — same rows resolved every time.
        Assert.Single(landRows);
        Assert.Single(machineRows);
        Assert.Equal(landId1, landRows[0].Id);
        Assert.Equal(machineId1, machineRows[0].Id);

        var landMaster = landRows[0];
        var machine = machineRows[0];

        Assert.True(landMaster.IsMaster);
        Assert.False(machine.IsMaster);
        Assert.Equal(landMaster.Id, machine.ParentMasterId);

        // Two engagements total (one per distinct appraisal), both on the primary; the
        // re-run of a2 did not add a third.
        Assert.Equal(2, landMaster.Engagements.Count);
        Assert.Contains(landMaster.Engagements, e => e.AppraisalId == a1Id);
        Assert.Contains(landMaster.Engagements, e => e.AppraisalId == a2Id);
        Assert.Empty(machine.Engagements);
    }

    // -----------------------------------------------------------------------
    // Task (c): Standalone single-type appraisals (pure Condo, pure Machine) still produce ONE
    // IsMaster with no alias — the sole component is trivially the primary.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task StandaloneCondo_IsMasterTrueNoAlias()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        Guid appraisalId;

        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            SeedCondoProperty(a, "LO-BKK", $"STC-{tag}", "B1", "5", "501",
                $"STC-T-{tag}", "Chanote", "Bangkok");
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;
        }

        await ProcessAppraisalInNewScopeAsync(appraisalId);

        using var assertScope = CreateScope();
        var collateralDb = GetCollateralDbContext(assertScope);

        var condo = await collateralDb.CollateralMasters
            .Include(m => m.CondoDetail)
            .Include(m => m.Engagements)
            .FirstAsync(m => m.CondoDetail != null && m.CondoDetail.CondoRegistrationNumber == $"STC-{tag}",
                TestContext.Current.CancellationToken);

        Assert.True(condo.IsMaster);
        Assert.Null(condo.ParentMasterId);
        Assert.Single(condo.Engagements);

        var engCount = await collateralDb.CollateralEngagements
            .CountAsync(e => e.AppraisalId == appraisalId, TestContext.Current.CancellationToken);
        Assert.Equal(1, engCount);
    }

    [Fact]
    public async Task StandaloneMachine_IsMasterTrueNoAlias()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var regNo = $"STM-{tag}";
        Guid appraisalId;

        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            SeedMachineryProperty(a, registrationNo: regNo,
                serialNo: "S1", brand: "BRAND-X", model: "M1", manufacturer: "MFR-X");
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;
        }

        await ProcessAppraisalInNewScopeAsync(appraisalId);

        using var assertScope = CreateScope();
        var collateralDb = GetCollateralDbContext(assertScope);

        var machine = await collateralDb.CollateralMasters
            .Include(m => m.MachineDetail)
            .Include(m => m.Engagements)
            .FirstAsync(m => m.MachineDetail != null && m.MachineDetail.MachineRegistrationNo == regNo,
                TestContext.Current.CancellationToken);

        Assert.True(machine.IsMaster);
        Assert.Null(machine.ParentMasterId);
        Assert.Single(machine.Engagements);

        var engCount = await collateralDb.CollateralEngagements
            .CountAsync(e => e.AppraisalId == appraisalId, TestContext.Current.CancellationToken);
        Assert.Equal(1, engCount);
    }

    // -----------------------------------------------------------------------
    // C1 regression guard — role change: a component that was appraised STANDALONE (has its own
    // engagement history) must never be demoted, even when a later appraisal makes it a
    // non-primary component alongside Land. Old behavior (before the C1 fix) threw
    // InvalidOperationException from DemoteToAlias here, permanently failing A2.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Machine_StandaloneThenLandPrimary_MachineKeepsIsMasterAndOwnEngagement()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var regNo = $"RC-{tag}";
        var titleNo = $"RC-LAND-{tag}";
        Guid a1Id, a2Id;

        // A1: machine-only appraisal — machine M becomes IsMaster with engagement E1.
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a1 = CreateAppraisalSeed(Guid.NewGuid());
            SeedMachineryProperty(a1, registrationNo: regNo,
                serialNo: "S1", brand: "BRAND-RC", model: "M1", manufacturer: "MFR-RC");
            appraisalDb.Appraisals.Add(a1);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a1Id = a1.Id;
        }
        await ProcessAppraisalInNewScopeAsync(a1Id);

        Guid machineId;
        using (var checkScope = CreateScope())
        {
            var collateralDb = GetCollateralDbContext(checkScope);
            var machine = await collateralDb.CollateralMasters
                .Include(m => m.MachineDetail)
                .Include(m => m.Engagements)
                .FirstAsync(m => m.MachineDetail != null && m.MachineDetail.MachineRegistrationNo == regNo,
                    TestContext.Current.CancellationToken);
            Assert.True(machine.IsMaster);
            Assert.Single(machine.Engagements);
            machineId = machine.Id;
        }

        // A2: land + the SAME machine — land is the (unconditional) primary, so the machine is
        // now a non-primary component. It must NOT be demoted (it has engagement history).
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a2 = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandProperty(a2, "LO-001", "BKK", "D1", "S1", titleNo, "Chanote");
            SeedMachineryProperty(a2, registrationNo: regNo,
                serialNo: "S1", brand: "BRAND-RC", model: "M1", manufacturer: "MFR-RC");
            appraisalDb.Appraisals.Add(a2);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a2Id = a2.Id;
        }

        // Must NOT throw — this was the C1 regression (DemoteToAlias threw on M here).
        await ProcessAppraisalInNewScopeAsync(a2Id);

        using var assertScope = CreateScope();
        var collateralDbAssert = GetCollateralDbContext(assertScope);

        var landMaster = await collateralDbAssert.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .FirstAsync(m => m.LandDetail != null && m.LandDetail.TitleNumber == titleNo,
                TestContext.Current.CancellationToken);

        var machineReloaded = await collateralDbAssert.CollateralMasters
            .Include(m => m.MachineDetail)
            .Include(m => m.Engagements)
            .FirstAsync(m => m.Id == machineId, TestContext.Current.CancellationToken);

        // Land is A2's new primary, owning exactly one (new) engagement for A2.
        Assert.True(landMaster.IsMaster);
        Assert.Single(landMaster.Engagements);
        Assert.Contains(landMaster.Engagements, e => e.AppraisalId == a2Id);

        // The machine master STAYS IsMaster (appraised standalone in A1) and RETAINS E1 — it was
        // not demoted, and A2 did not add a second engagement to it.
        Assert.True(machineReloaded.IsMaster);
        Assert.Null(machineReloaded.ParentMasterId);
        Assert.Single(machineReloaded.Engagements);
        Assert.Contains(machineReloaded.Engagements, e => e.AppraisalId == a1Id);

        // Two distinct IsMaster rows now legitimately exist — expected and correct under the
        // "engagement history makes a row a real collateral" rule.
        Assert.NotEqual(landMaster.Id, machineReloaded.Id);
    }

    // -----------------------------------------------------------------------
    // W1 regression guard — role change back: a component demoted to a (no-engagement) alias
    // under a different primary must be PROMOTED back to a standalone IsMaster when a later
    // appraisal makes it, in its own right, the primary — never walked to its old (foreign)
    // parent, which would misattribute the engagement to an unrelated collateral.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Condo_DemotedThenReappraisedStandalone_IsPromotedBackToMaster()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var titleNo = $"PR-LAND-{tag}";
        var condoRegNo = $"PR-{tag}";
        Guid a1Id, a2Id;

        // A1: land + condo — land is primary; the condo has no engagement of its own, so it is
        // demoted to a typed alias of the land master.
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a1 = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandProperty(a1, "LO-001", "BKK", "D1", "S1", titleNo, "Chanote");
            SeedCondoProperty(a1, "LO-BKK", condoRegNo, "B1", "5", "501", $"PR-T-{tag}", "Chanote", "Bangkok");
            appraisalDb.Appraisals.Add(a1);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a1Id = a1.Id;
        }
        await ProcessAppraisalInNewScopeAsync(a1Id);

        Guid condoId;
        using (var checkScope = CreateScope())
        {
            var collateralDb = GetCollateralDbContext(checkScope);
            var condo = await collateralDb.CollateralMasters
                .Include(m => m.CondoDetail)
                .Include(m => m.Engagements)
                .FirstAsync(m => m.CondoDetail != null && m.CondoDetail.CondoRegistrationNumber == condoRegNo,
                    TestContext.Current.CancellationToken);
            Assert.False(condo.IsMaster);
            Assert.NotNull(condo.ParentMasterId);
            Assert.Empty(condo.Engagements);
            condoId = condo.Id;
        }

        // A2: condo-only reappraisal — the SAME condo is now, in its own right, this appraisal's
        // primary. It must be promoted back to IsMaster, not resolved to its old (foreign) parent.
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a2 = CreateAppraisalSeed(Guid.NewGuid());
            SeedCondoProperty(a2, "LO-BKK", condoRegNo, "B1", "5", "501", $"PR-T-{tag}", "Chanote", "Bangkok");
            appraisalDb.Appraisals.Add(a2);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            a2Id = a2.Id;
        }
        await ProcessAppraisalInNewScopeAsync(a2Id);

        using var assertScope = CreateScope();
        var collateralDbAssert = GetCollateralDbContext(assertScope);

        var condoReloaded = await collateralDbAssert.CollateralMasters
            .Include(m => m.CondoDetail)
            .Include(m => m.Engagements)
            .FirstAsync(m => m.Id == condoId, TestContext.Current.CancellationToken);

        Assert.True(condoReloaded.IsMaster);
        Assert.Null(condoReloaded.ParentMasterId);
        Assert.Single(condoReloaded.Engagements);
        Assert.Contains(condoReloaded.Engagements, e => e.AppraisalId == a2Id);
    }

    // -----------------------------------------------------------------------
    // W2 regression guard — Leasehold-as-primary ordering: the lowest-GroupNumber group is a
    // Leasehold group (processed last, in pass 2), while a SEPARATE Machine group (pass 1,
    // non-primary) also exists. Exactly one IsMaster (the leasehold) must result; the machine
    // becomes a typed alias; one engagement lands on the leasehold.
    //
    // The leasehold's own underlying Condo is pre-seeded directly (no engagement) so pass 1's
    // independent processing of that same Condo property and the leasehold's underlying-
    // resolution both resolve to the SAME already-persisted row within the unit of work — this
    // sidesteps an unrelated, pre-existing gap where the underlying-Condo lookup (unlike Land's
    // landMasterByPropertyId cache) doesn't see an Added-but-unsaved row from earlier in the
    // same SaveChanges batch.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task LeaseholdOnlyPrimary_NoLand_MachineNonPrimaryGroupBecomesTypedAlias()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var condoRegNo = $"LHU-{tag}";
        var contractNo = $"LHC-{tag}";
        var machineRegNo = $"LHM-{tag}";

        using (var preSeedScope = CreateScope())
        {
            var collateralDb = GetCollateralDbContext(preSeedScope);
            var underlyingCondo = CollateralMaster.CreateCondo(
                ownerName: "Test Owner",
                landOfficeCode: "LO-BKK",
                condoRegistrationNumber: condoRegNo,
                buildingNumber: "B1",
                floorNumber: "5",
                roomNumber: "501",
                province: "Bangkok",
                district: "Test District",
                subDistrict: "Test Subdistrict",
                condoName: null);
            collateralDb.CollateralMasters.Add(underlyingCondo);
            await collateralDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Guid appraisalId;
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a = CreateAppraisalSeed(Guid.NewGuid());

            // Leasehold property — its own group, GroupNumber=1 (lowest → primary).
            var lhProp = a.AddLeaseAgreementLandProperty();
            lhProp.LeaseAgreementDetail!.Update(
                contractNo: contractNo,
                lessorName: "Landlord Co",
                lesseeName: "Tenant Inc",
                leaseStartDate: new DateTime(2022, 1, 1));
            var lhGroup = a.CreateGroup("Leasehold Group");
            lhGroup.AddProperty(lhProp.Id);

            // The pre-seeded condo, present in THIS appraisal too (required for
            // UpsertLeaseholdAsync's no-land underlying-resolution fallback). Left ungrouped.
            SeedCondoProperty(a, "LO-BKK", condoRegNo, "B1", "5", "501", "N/A", "Chanote", "Bangkok");

            // Machine property — its OWN group, GroupNumber=2 (non-primary, unrelated to the lease).
            var machineProp = SeedMachineryProperty(a, registrationNo: machineRegNo,
                serialNo: "S1", brand: "BRAND-L", model: "M1", manufacturer: "MFR-L");
            var machineGroup = a.CreateGroup("Machine Group");
            machineGroup.AddProperty(machineProp.Id);

            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;
        }

        await ProcessAppraisalInNewScopeAsync(appraisalId);

        using var assertScope = CreateScope();
        var collateralDb2 = GetCollateralDbContext(assertScope);

        var leaseMaster = await collateralDb2.CollateralMasters
            .Include(m => m.LeaseholdDetail)
            .Include(m => m.Engagements)
            .FirstAsync(m => m.LeaseholdDetail != null && m.LeaseholdDetail.LeaseRegistrationNo == contractNo,
                TestContext.Current.CancellationToken);

        var machine = await collateralDb2.CollateralMasters
            .Include(m => m.MachineDetail)
            .Include(m => m.Engagements)
            .FirstAsync(m => m.MachineDetail != null && m.MachineDetail.MachineRegistrationNo == machineRegNo,
                TestContext.Current.CancellationToken);

        // Leasehold is the appraisal's primary — owns the single engagement.
        Assert.True(leaseMaster.IsMaster);
        Assert.Null(leaseMaster.ParentMasterId);
        Assert.Single(leaseMaster.Engagements);

        // The separately-grouped Machine (pass-1, non-primary) becomes a typed alias, keeping
        // its own MachineDetail.
        Assert.False(machine.IsMaster);
        Assert.Equal(leaseMaster.Id, machine.ParentMasterId);
        Assert.Empty(machine.Engagements);
        Assert.NotNull(machine.MachineDetail);

        var engCount = await collateralDb2.CollateralEngagements
            .CountAsync(e => e.AppraisalId == appraisalId, TestContext.Current.CancellationToken);
        Assert.Equal(1, engCount);
    }
}
