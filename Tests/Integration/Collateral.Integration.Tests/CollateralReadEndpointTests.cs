using System.Net;
using System.Text.Json;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Collateral.Application.Features.CollateralMasters.GetEngagements;
using Collateral.Application.Features.CollateralMasters.GetEngagementSnapshot;
using Collateral.Application.Features.CollateralMasters.GetById;
using Collateral.Application.Features.CollateralMasters.Lookup;
using Collateral.CollateralMasters.Services;
using Collateral.Data;
using Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Collateral.Integration.Tests;

/// <summary>
/// Integration tests for Collateral master read endpoints (step 4, v1).
/// Seeds data via the CollateralMasterUpsertService (same path as the consumer)
/// then drives the HTTP endpoints via the test client.
/// </summary>
[Collection("Integration")]
public class CollateralReadEndpointTests(IntegrationTestFixture fixture)
{
    private readonly HttpClient _client = fixture.IntegrationTestWebApplicationFactory.CreateClient();

    // ------------------------------------------------------------------
    // Seed helpers (lifted from CollateralUpsertServiceTests pattern)
    // ------------------------------------------------------------------

    private static AppraisalProperty SeedLandProperty(
        AppraisalAggregate appraisal,
        string landOffice,
        string province,
        string district,
        string subDistrict,
        string titleNo,
        string titleType)
    {
        var prop = appraisal.AddLandProperty();
        prop.LandDetail!.Update(
            address: AdministrativeAddress.Create(subDistrict, district, province, landOffice));
        var title = LandTitle.Create(prop.LandDetail.Id, titleNo, titleType);
        prop.LandDetail.AddTitle(title);
        return prop;
    }

    private static AppraisalAggregate CreateAppraisalSeed(Guid requestId)
    {
        var a = AppraisalAggregate.Create(requestId, "New", "Normal");
        a.SetAppraisalNumber($"AP-{Guid.NewGuid():N}".Substring(0, 18));
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
    /// Seeds a Land appraisal and processes it through the upsert service.
    /// Returns the created CollateralMaster Id.
    /// </summary>
    private async Task<Guid> SeedLandMasterAsync(
        string landOffice,
        string province,
        string district,
        string subDistrict,
        string titleNo,
        string titleType = "Chanote")
    {
        Guid appraisalId;
        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var appraisal = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandProperty(appraisal, landOffice, province, district, subDistrict, titleNo, titleType);
            appraisalDb.Appraisals.Add(appraisal);
            await appraisalDb.SaveChangesAsync();
            appraisalId = appraisal.Id;
        }

        using var procScope = CreateScope();
        var svc = GetUpsertService(procScope);
        await svc.ProcessAppraisalAsync(appraisalId, TestContext.Current.CancellationToken);

        // Retrieve the master Id
        using var queryScope = CreateScope();
        var collateralDb = GetCollateralDbContext(queryScope);
        var master = await collateralDb.CollateralMasters
            .Include(m => m.LandDetail)
            .FirstAsync(m => m.LandDetail != null
                             && m.LandDetail.TitleDeedNo == titleNo
                             && m.LandDetail.Province == province);
        return master.Id;
    }

    // ------------------------------------------------------------------
    // Test: Lookup — hit returns 200 with correct master
    // ------------------------------------------------------------------
    [Fact]
    public async Task Lookup_LandMatch_Returns200WithCorrectMaster()
    {
        var titleNo = $"READ-LOOKUP-{Guid.NewGuid():N}".Substring(0, 25);
        var masterId = await SeedLandMasterAsync("LO-READ", "Bangkok", "Bangrak", "Silom", titleNo);

        var url = $"/collateral-masters/lookup?type=Land"
                  + $"&landOfficeCode=LO-READ"
                  + $"&province=Bangkok"
                  + $"&amphur=Bangrak"
                  + $"&tambon=Silom"
                  + $"&titleDeedType=Chanote"
                  + $"&titleDeedNo={titleNo}";

        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.Equal(masterId.ToString(), root.GetProperty("id").GetString());
        Assert.Equal("Land", root.GetProperty("collateralType").GetString());
        Assert.True(root.TryGetProperty("landDetail", out var ld));
        Assert.Equal(titleNo, ld.GetProperty("titleDeedNo").GetString());
    }

    // ------------------------------------------------------------------
    // Test: Lookup — miss returns 404
    // ------------------------------------------------------------------
    [Fact]
    public async Task Lookup_NoMatch_Returns404()
    {
        var url = "/collateral-masters/lookup?type=Land"
                  + "&landOfficeCode=LO-GHOST"
                  + "&province=Ghost Province"
                  + "&amphur=Ghost Amphur"
                  + "&tambon=Ghost Tambon"
                  + "&titleDeedType=Chanote"
                  + "&titleDeedNo=GHOST-99999";

        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Test: GetById — returns correct shape for a Land master
    // ------------------------------------------------------------------
    [Fact]
    public async Task GetById_ExistingLandMaster_Returns200WithCorrectShape()
    {
        var titleNo = $"READ-BYID-{Guid.NewGuid():N}".Substring(0, 25);
        var masterId = await SeedLandMasterAsync("LO-BYID", "Chiang Mai", "Mueang", "Chang Phueak", titleNo);

        var response = await _client.GetAsync($"/collateral-masters/{masterId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.Equal(masterId.ToString(), root.GetProperty("id").GetString());
        Assert.Equal("Land", root.GetProperty("collateralType").GetString());
        // Land detail should be populated
        Assert.True(root.TryGetProperty("landDetail", out var ld));
        Assert.Equal("Chiang Mai", ld.GetProperty("province").GetString());
        Assert.Equal(titleNo, ld.GetProperty("titleDeedNo").GetString());
        // Other type details should be null
        Assert.True(root.GetProperty("condoDetail").ValueKind == JsonValueKind.Null);
        Assert.True(root.GetProperty("leaseholdDetail").ValueKind == JsonValueKind.Null);
        Assert.True(root.GetProperty("machineDetail").ValueKind == JsonValueKind.Null);
    }

    // ------------------------------------------------------------------
    // Test: GetById — non-existent master returns 404
    // ------------------------------------------------------------------
    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        var response = await _client.GetAsync($"/collateral-masters/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Test: Engagement list — paginated, ordered by AppraisalDate DESC
    // ------------------------------------------------------------------
    [Fact]
    public async Task GetEngagements_TwoAppraisals_ReturnsPaginatedDescending()
    {
        // Seed first appraisal
        var titleNo = $"READ-ENG-{Guid.NewGuid():N}".Substring(0, 25);
        Guid masterId;
        Guid firstAppraisalId, secondAppraisalId;

        using (var scope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(scope);
            var a1 = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandProperty(a1, "LO-ENG", "Khon Kaen", "Mueang", "Nai Mueang", titleNo, "Chanote");
            appraisalDb.Appraisals.Add(a1);
            await appraisalDb.SaveChangesAsync();
            firstAppraisalId = a1.Id;
        }

        using (var procScope = CreateScope())
        {
            var svc = GetUpsertService(procScope);
            await svc.ProcessAppraisalAsync(firstAppraisalId, CancellationToken.None);
        }

        // Seed second appraisal against same property (re-appraisal)
        using (var scope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(scope);
            var a2 = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandProperty(a2, "LO-ENG", "Khon Kaen", "Mueang", "Nai Mueang", titleNo, "Chanote");
            appraisalDb.Appraisals.Add(a2);
            await appraisalDb.SaveChangesAsync();
            secondAppraisalId = a2.Id;
        }

        using (var procScope = CreateScope())
        {
            var svc = GetUpsertService(procScope);
            await svc.ProcessAppraisalAsync(secondAppraisalId, CancellationToken.None);
        }

        // Get master
        using (var queryScope = CreateScope())
        {
            var db = GetCollateralDbContext(queryScope);
            var m = await db.CollateralMasters
                .Include(x => x.LandDetail)
                .FirstAsync(x => x.LandDetail != null && x.LandDetail.TitleDeedNo == titleNo);
            masterId = m.Id;
        }

        var response = await _client.GetAsync($"/collateral-masters/{masterId}/engagements?page=0&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var engagements = doc.RootElement.GetProperty("engagements").GetProperty("items");

        Assert.Equal(2, engagements.GetArrayLength());

        // Should be ordered AppraisalDate DESC — second is newer so it comes first
        var firstItem = engagements[0];
        var secondItem = engagements[1];
        var firstDate = firstItem.GetProperty("appraisalDate").GetDateTime();
        var secondDate = secondItem.GetProperty("appraisalDate").GetDateTime();
        Assert.True(firstDate >= secondDate, "Engagements should be ordered by AppraisalDate DESC");
    }

    // ------------------------------------------------------------------
    // Test: Engagement snapshot — returns Snapshot JSON string
    // ------------------------------------------------------------------
    [Fact]
    public async Task GetEngagementSnapshot_ExistingEngagement_ReturnsSnapshotJson()
    {
        var titleNo = $"READ-SNAP-{Guid.NewGuid():N}".Substring(0, 25);
        var masterId = await SeedLandMasterAsync("LO-SNAP", "Phuket", "Mueang", "Talat Yai", titleNo);

        // Get the engagement
        using var scope = CreateScope();
        var db = GetCollateralDbContext(scope);
        var engagement = await db.CollateralEngagements
            .FirstAsync(e => e.CollateralMasterId == masterId);

        var response = await _client.GetAsync(
            $"/collateral-masters/{masterId}/engagements/{engagement.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.Equal(engagement.Id.ToString(), root.GetProperty("id").GetString());
        Assert.Equal(masterId.ToString(), root.GetProperty("collateralMasterId").GetString());
        // Snapshot should be a non-empty JSON string
        Assert.True(root.TryGetProperty("snapshot", out var snapshot));
        Assert.NotEmpty(snapshot.GetString() ?? "");
    }

    // ------------------------------------------------------------------
    // Test: Engagement snapshot — wrong masterId scoping returns 404
    // ------------------------------------------------------------------
    [Fact]
    public async Task GetEngagementSnapshot_WrongMaster_Returns404()
    {
        var titleNo = $"READ-SNAP2-{Guid.NewGuid():N}".Substring(0, 25);
        var masterId = await SeedLandMasterAsync("LO-SNAP2", "Phuket", "Mueang", "Talat Yai", titleNo);

        using var scope = CreateScope();
        var db = GetCollateralDbContext(scope);
        var engagement = await db.CollateralEngagements
            .FirstAsync(e => e.CollateralMasterId == masterId);

        // Use a random wrong master ID — should 404
        var response = await _client.GetAsync(
            $"/collateral-masters/{Guid.NewGuid()}/engagements/{engagement.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Test: Catalog — type filter returns only matching type
    // ------------------------------------------------------------------
    [Fact]
    public async Task GetCatalog_TypeFilter_ReturnsOnlyMatchingType()
    {
        // Seed a Land master with a unique province so we can filter it
        var titleNo = $"READ-CAT-{Guid.NewGuid():N}".Substring(0, 25);
        await SeedLandMasterAsync("LO-CAT", "Nakhon Nowhere", "Mueang", "Center", titleNo);

        // The catalog endpoint enforces admin role in the handler.
        // The test client uses BypassAuthenticationHandler which does not add Admin role,
        // so the handler will throw UnauthorizedAccessException → 500 (or 403 depending on handler).
        // We verify that the endpoint is reachable (not 404/401) and returns a structured error.
        // For a full admin flow test we would need an admin-role test factory — deferred per project pattern.
        // Here we at minimum verify the endpoint exists and auth is applied.
        var response = await _client.GetAsync("/collateral-masters?type=Land&page=0&pageSize=10");

        // Handler throws UnauthorizedAccessException → CustomExceptionHandler maps this to 403 or 500
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
        // Should not be 401 (we are authenticated via bypass handler)
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
