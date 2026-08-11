using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Collateral.CollateralMasters.Models;
using Collateral.CollateralMasters.Services;
using Collateral.Contracts.ConstructionInspection;
using Collateral.Contracts.Engagements;
using Collateral.Data;
using Integration.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;
using Address = Appraisal.Domain.Appraisals.Address;

namespace Integration.Collateral.Integration.Tests;

/// <summary>
/// PR-5 integration tests covering:
///   1. Single CI — snapshot contains exactly 1 entry in constructionInspections[].
///   2. Multi-building case — 3 buildings/CIs: snapshot contains all 3 entries.
///   3. Schema — LandDetail no longer has LastConstructionInspectionId (via reflection).
///   4. GetMostRecentEngagementByPriorAppraisalQuery — returns engagement company via master link.
///   5. GetConstructionInspectionFeeForAppraisalQuery — returns fee from engagement.
/// </summary>
[Collection("Integration")]
public class CollateralPR5_ConstructionInspectionsTests(IntegrationTestFixture fixture)
{
    // -----------------------------------------------------------------------
    // Seed helpers (mirrors CollateralPhaseC_PR2Tests pattern)
    // -----------------------------------------------------------------------

    private static AppraisalAggregate CreateAppraisalSeed(Guid requestId)
    {
        var a = AppraisalAggregate.Create(requestId, "New", "Normal", DateTime.Now);
        a.SetAppraisalNumber($"AP-PR5-{Guid.NewGuid():N}"[..18]);
        typeof(AppraisalAggregate)
            .GetProperty("CompletedAt")!
            .SetValue(a, DateTime.UtcNow);
        return a;
    }

    private static AppraisalProperty SeedLandProperty(
        AppraisalAggregate appraisal,
        string landOffice, string province, string district, string subDistrict,
        string titleNo, string titleType,
        ConstructionInspection? inspection = null)
    {
        var prop = appraisal.AddLandProperty();
        prop.LandDetail!.Update(
            address: Address.Create(subDistrict, district, province), landOffice: landOffice);
        var title = LandTitle.Create(prop.LandDetail.Id, titleNo, titleType);
        prop.LandDetail.AddTitle(title);
        if (inspection is not null)
            prop.SetConstructionInspection(inspection);
        return prop;
    }

    private static ConstructionInspection CreateSummaryInspection(Guid propertyId, decimal progressPct)
    {
        var ci = ConstructionInspection.CreateSummary(
            propertyId,
            totalValue: 1_000_000m,
            summaryDetail: "Test CI",
            summaryPreviousProgressPct: 0m,
            summaryPreviousValue: 0m,
            summaryCurrentProgressPct: progressPct,
            summaryCurrentValue: progressPct / 100m * 1_000_000m,
            remark: null);
        // Fix AppraisalPropertyId via reflection (it defaults to propertyId already)
        return ci;
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
    // Test PR5-1: Single inspection — snapshot constructionInspections[] has exactly 1 entry
    // -----------------------------------------------------------------------
    [Fact]
    public async Task PR5_1_SingleCI_SnapshotHasExactlyOneInspectionEntry()
    {
        var titleNo = "PR5-1CI-" + Guid.NewGuid().ToString("N")[..6];
        Guid appraisalId;
        Guid propertyId;

        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a = CreateAppraisalSeed(Guid.NewGuid());

            var prop = a.AddLandProperty();
            prop.LandDetail!.Update(
                address: Address.Create("Silom", "Bangrak", "Bangkok"), landOffice: "LO-BKK");
            var title = LandTitle.Create(prop.LandDetail.Id, titleNo, "Chanote");
            prop.LandDetail.AddTitle(title);

            var ci = CreateSummaryInspection(prop.Id, 55m);
            prop.SetConstructionInspection(ci);

            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;
            // Capture Id AFTER save — EF populates it from NEWSEQUENTIALID()
            propertyId = prop.Id;
        }

        await ProcessAppraisalInNewScopeAsync(appraisalId);

        using var assertScope = CreateScope();
        var collateralDb = GetCollateralDbContext(assertScope);

        var master = await collateralDb.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .FirstOrDefaultAsync(m => m.LandDetail != null && m.LandDetail.TitleNumber == titleNo,
                TestContext.Current.CancellationToken);

        Assert.NotNull(master);
        var engagement = master.Engagements.Single();
        Assert.NotNull(engagement.Snapshot);

        using var doc = JsonDocument.Parse(engagement.Snapshot);
        var root = doc.RootElement;

        // PR-4: constructionInspections now lives inside groups[0].constructionInspections
        Assert.True(root.TryGetProperty("groups", out var groupsArray), "Snapshot must contain 'groups' array");
        Assert.Equal(JsonValueKind.Array, groupsArray.ValueKind);
        var firstGroup = groupsArray.EnumerateArray().First();

        Assert.True(firstGroup.TryGetProperty("constructionInspections", out var ciArray),
            "groups[0] must contain 'constructionInspections' array");
        Assert.Equal(JsonValueKind.Array, ciArray.ValueKind);

        var entries = ciArray.EnumerateArray().ToList();
        Assert.Single(entries);

        // Each entry must carry a non-empty inspectionId (DB-assigned via NEWSEQUENTIALID())
        Assert.True(entries[0].TryGetProperty("inspectionId", out var idElem));
        Assert.NotEqual(Guid.Empty.ToString(), idElem.GetString());

        // Each entry must also have propertyId (PR-5 addition)
        Assert.True(entries[0].TryGetProperty("propertyId", out var pidElem),
            "constructionInspections entry must carry propertyId");
        Assert.Equal(propertyId.ToString(), pidElem.GetString());
    }

    // -----------------------------------------------------------------------
    // Test PR5-2: Multi-building — 3 CIs → snapshot constructionInspections[] has 3 entries
    //
    // This is the regression that the single-valued LastConstructionInspectionId column
    // caused: only one inspection id was preserved per land master. Here we verify all
    // three are captured in the engagement snapshot.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task PR5_2_MultiBuilding_ThreeCIs_SnapshotHasAllThreeEntries()
    {
        var titleNo = "PR5-3CI-" + Guid.NewGuid().ToString("N")[..6];
        Guid appraisalId;
        Guid landPropId, bldg1Id, bldg2Id;

        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a = CreateAppraisalSeed(Guid.NewGuid());

            // Land property with a CI
            var landProp = a.AddLandProperty();
            landProp.LandDetail!.Update(
                address: Address.Create("Wattana", "Wattana", "Bangkok"), landOffice: "LO-BKK2");
            var title = LandTitle.Create(landProp.LandDetail.Id, titleNo, "Chanote");
            landProp.LandDetail.AddTitle(title);

            var landCi = CreateSummaryInspection(landProp.Id, 30m);
            landProp.SetConstructionInspection(landCi);

            // Building 1 (BuiltOnTitleNumber matches the land title) with its own CI
            var bldg1 = a.AddBuildingProperty();
            bldg1.BuildingDetail!.Update(builtOnTitleNumber: titleNo);
            var b1Ci = CreateSummaryInspection(bldg1.Id, 50m);
            bldg1.SetConstructionInspection(b1Ci);

            // Building 2 (same land) with its own CI
            var bldg2 = a.AddBuildingProperty();
            bldg2.BuildingDetail!.Update(builtOnTitleNumber: titleNo);
            var b2Ci = CreateSummaryInspection(bldg2.Id, 80m);
            bldg2.SetConstructionInspection(b2Ci);

            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;
            // Capture IDs AFTER save — EF populates them from NEWSEQUENTIALID()
            landPropId = landProp.Id;
            bldg1Id = bldg1.Id;
            bldg2Id = bldg2.Id;
        }

        await ProcessAppraisalInNewScopeAsync(appraisalId);

        using var assertScope = CreateScope();
        var collateralDb = GetCollateralDbContext(assertScope);

        var master = await collateralDb.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .FirstOrDefaultAsync(m => m.LandDetail != null && m.LandDetail.TitleNumber == titleNo,
                TestContext.Current.CancellationToken);

        Assert.NotNull(master);
        var engagement = master.Engagements.Single();
        Assert.NotNull(engagement.Snapshot);

        using var doc = JsonDocument.Parse(engagement.Snapshot);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("groups", out var groupsArray2),
            "Snapshot must contain 'groups' array");
        var firstGroup2 = groupsArray2.EnumerateArray().First();
        Assert.True(firstGroup2.TryGetProperty("constructionInspections", out var ciArray),
            "First group must contain 'constructionInspections' array");

        var entries = ciArray.EnumerateArray().ToList();

        // CRITICAL: all 3 inspections must be preserved — this was the lossy bug.
        // The old single-valued LastConstructionInspectionId column could only store one.
        Assert.Equal(3, entries.Count);

        // Verify all three property IDs are represented — each entry has a distinct property
        var snapshotPropertyIds = entries
            .Select(e => e.GetProperty("propertyId").GetString())
            .ToHashSet();

        Assert.Contains(landPropId.ToString(), snapshotPropertyIds);
        Assert.Contains(bldg1Id.ToString(), snapshotPropertyIds);
        Assert.Contains(bldg2Id.ToString(), snapshotPropertyIds);

        // Each entry must have a non-empty inspectionId (DB-assigned)
        foreach (var entry in entries)
        {
            Assert.True(entry.TryGetProperty("inspectionId", out var idElem));
            Assert.NotEqual(Guid.Empty.ToString(), idElem.GetString());
        }
    }

    // -----------------------------------------------------------------------
    // Test PR5-3: Schema — LandDetail does NOT have LastConstructionInspectionId
    // -----------------------------------------------------------------------
    [Fact]
    public void PR5_3_LandDetailModel_DoesNotHaveLastConstructionInspectionIdProperty()
    {
        var prop = typeof(LandDetail).GetProperty("LastConstructionInspectionId");
        Assert.Null(prop);
    }

    // -----------------------------------------------------------------------
    // Test PR5-4: GetMostRecentEngagementByPriorAppraisalQuery — returns company from engagement
    // -----------------------------------------------------------------------
    [Fact]
    public async Task PR5_4_GetMostRecentEngagementByPriorAppraisal_ReturnsCompanyFromEngagement()
    {
        var titleNo = "PR5-CO-" + Guid.NewGuid().ToString("N")[..6];
        Guid appraisalId;
        var expectedCompanyId = Guid.NewGuid();
        const string expectedCompanyName = "Test Appraisal Co. PR5";

        // Seed appraisal
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandProperty(a, "LO-PR5", "Bangkok", "D1", "S1", titleNo, "Chanote");
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;
        }

        await ProcessAppraisalInNewScopeAsync(appraisalId);

        // Manually patch the engagement's company fields (simulating what the consumer does when
        // the assignment has a company — in test there is no assignment so we patch directly)
        using (var patchScope = CreateScope())
        {
            var db = GetCollateralDbContext(patchScope);
            var engagement = await db.CollateralEngagements
                .FirstAsync(e => e.AppraisalId == appraisalId, TestContext.Current.CancellationToken);

            // Patch via reflection (columns are private-set)
            typeof(CollateralEngagement)
                .GetProperty("AppraisalCompanyId")!
                .SetValue(engagement, expectedCompanyId);
            typeof(CollateralEngagement)
                .GetProperty("AppraisalCompanyName")!
                .SetValue(engagement, expectedCompanyName);

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Query via the handler
        using var queryScope = CreateScope();
        var mediator = queryScope.ServiceProvider.GetRequiredService<IMediator>();
        var result = await mediator.Send(
            new GetMostRecentEngagementByPriorAppraisalQuery(appraisalId),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(appraisalId, result!.AppraisalId);
        Assert.Equal(expectedCompanyId, result.CompanyId);
        Assert.Equal(expectedCompanyName, result.CompanyName);
    }

    // -----------------------------------------------------------------------
    // Test PR5-5: GetConstructionInspectionFeeForAppraisalQuery — returns fee from engagement
    // -----------------------------------------------------------------------
    [Fact]
    public async Task PR5_5_GetConstructionInspectionFeeForAppraisal_ReturnsFeeFromEngagement()
    {
        var titleNo = "PR5-FEE-" + Guid.NewGuid().ToString("N")[..6];
        Guid appraisalId;
        const decimal expectedFee = 15_000m;

        // Seed appraisal
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandProperty(a, "LO-PR5", "Bangkok", "D1", "S1", titleNo, "Chanote");
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;
        }

        await ProcessAppraisalInNewScopeAsync(appraisalId);

        // Manually patch the engagement's CI fee (simulating the fee seeded from AppraisalFee)
        using (var patchScope = CreateScope())
        {
            var db = GetCollateralDbContext(patchScope);
            var engagement = await db.CollateralEngagements
                .FirstAsync(e => e.AppraisalId == appraisalId, TestContext.Current.CancellationToken);

            typeof(CollateralEngagement)
                .GetProperty("ConstructionInspectionFeeAmount")!
                .SetValue(engagement, expectedFee);

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Query via the handler
        using var queryScope = CreateScope();
        var mediator = queryScope.ServiceProvider.GetRequiredService<IMediator>();
        var fee = await mediator.Send(
            new GetConstructionInspectionFeeForAppraisalQuery(appraisalId),
            TestContext.Current.CancellationToken);

        Assert.NotNull(fee);
        Assert.Equal(expectedFee, fee.Value);
    }
}
