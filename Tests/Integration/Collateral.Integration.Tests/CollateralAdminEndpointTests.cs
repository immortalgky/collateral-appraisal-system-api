using System.Net;
using System.Net.Http.Json;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Collateral.Application.Features.CollateralMasters.EditMaster;
using Collateral.Application.Features.CollateralMasters.RestoreMaster;
using Collateral.Application.Features.CollateralMasters.SoftDeleteMaster;
using Collateral.CollateralMasters.Models;
using Collateral.CollateralMasters.Services;
using Collateral.Data;
using Collateral.Data.Repository;
using Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Collateral.Integration.Tests;

/// <summary>
/// Integration tests for admin endpoints: Edit (PATCH), SoftDelete (DELETE), Restore (POST …/restore).
/// Covers step 5 of the Collateral master module v1 plan (section 12 admin operations).
///
/// Note on role enforcement:
/// The test client uses BypassAuthenticationHandler which does NOT inject Admin roles.
/// The admin role-check in each handler throws UnauthorizedAccessException → 403.
/// Tests for happy-path and domain-logic paths (collision, RESTRICT) exercise the
/// domain and repository directly (bypassing HTTP auth) so we can actually execute the write.
/// </summary>
[Collection("Integration")]
public class CollateralAdminEndpointTests(IntegrationTestFixture fixture)
{
    private readonly HttpClient _client = fixture.IntegrationTestWebApplicationFactory.CreateClient();

    // ------------------------------------------------------------------
    // Shared seed helpers
    // ------------------------------------------------------------------

    private IServiceScope CreateScope()
        => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    private CollateralDbContext GetCollateralDbContext(IServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<CollateralDbContext>();

    private AppraisalDbContext GetAppraisalDbContext(IServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

    private ICollateralMasterUpsertService GetUpsertService(IServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<ICollateralMasterUpsertService>();

    private static AppraisalAggregate CreateAppraisalSeed(Guid requestId)
    {
        var a = AppraisalAggregate.Create(requestId, "New", "Normal", DateTime.Now);
        a.SetAppraisalNumber($"AP-{Guid.NewGuid():N}".Substring(0, 18));
        typeof(AppraisalAggregate)
            .GetProperty("CompletedAt")!
            .SetValue(a, DateTime.UtcNow);
        return a;
    }

    private static AppraisalProperty SeedLandProperty(
        AppraisalAggregate appraisal,
        string landOffice, string province, string district, string subDistrict,
        string titleNo, string titleType = "Chanote")
    {
        var prop = appraisal.AddLandProperty();
        prop.LandDetail!.Update(
            address: AdministrativeAddress.Create(subDistrict, district, province, landOffice));
        var title = LandTitle.Create(prop.LandDetail.Id, titleNo, titleType);
        prop.LandDetail.AddTitle(title);
        return prop;
    }

    private async Task<Guid> SeedLandMasterAsync(
        string landOffice, string province, string district, string subDistrict,
        string titleNo, string titleType = "Chanote")
    {
        Guid appraisalId;
        using (var scope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(scope);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandProperty(a, landOffice, province, district, subDistrict, titleNo, titleType);
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync();
            appraisalId = a.Id;
        }

        using (var scope = CreateScope())
        {
            var svc = GetUpsertService(scope);
            await svc.ProcessAppraisalAsync(appraisalId, TestContext.Current.CancellationToken);
        }

        using var queryScope = CreateScope();
        var collateralDb = GetCollateralDbContext(queryScope);
        var master = await collateralDb.CollateralMasters
            .Include(m => m.LandDetail)
            .FirstAsync(m => m.LandDetail != null
                             && m.LandDetail.TitleNumber == titleNo
                             && m.LandDetail.Province == province);
        return master.Id;
    }

    private ICollateralMasterRepository GetRepo(IServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<ICollateralMasterRepository>();

    // ------------------------------------------------------------------
    // Test Admin-01: PATCH — non-admin role gets 403
    // ------------------------------------------------------------------
    [Fact]
    public async Task Edit_NonAdminRole_Returns403()
    {
        var titleNo = $"ADMIN-01-{Guid.NewGuid():N}".Substring(0, 25);
        var masterId = await SeedLandMasterAsync("LO-A01", "Bangkok", "Bangrak", "Silom", titleNo);

        var body = new EditCollateralMasterRequest(
            OwnerName: "Updated Owner",
            Reason: "Test correction",
            LandDetail: null, CondoDetail: null, LeaseholdDetail: null, MachineDetail: null);

        var response = await _client.PatchAsJsonAsync($"/collateral-masters/{masterId}", body);

        // BypassAuthenticationHandler provides no Admin role → handler throws UnauthorizedAccessException → 403
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Test Admin-02: DELETE — non-admin role gets 403
    // ------------------------------------------------------------------
    [Fact]
    public async Task SoftDelete_NonAdminRole_Returns403()
    {
        var titleNo = $"ADMIN-02-{Guid.NewGuid():N}".Substring(0, 25);
        var masterId = await SeedLandMasterAsync("LO-A02", "Bangkok", "Bangrak", "Silom", titleNo);

        var response = await _client.DeleteAsync($"/collateral-masters/{masterId}?reason=Test+deletion");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Test Admin-03: POST restore — non-admin role gets 403
    // ------------------------------------------------------------------
    [Fact]
    public async Task Restore_NonAdminRole_Returns403()
    {
        var titleNo = $"ADMIN-03-{Guid.NewGuid():N}".Substring(0, 25);
        var masterId = await SeedLandMasterAsync("LO-A03", "Bangkok", "Bangrak", "Silom", titleNo);

        var body = new RestoreCollateralMasterRequest(Reason: "Test restore");
        var response = await _client.PostAsJsonAsync($"/collateral-masters/{masterId}/restore", body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Test Admin-04: Edit missing Reason — FluentValidation rejects
    // ------------------------------------------------------------------
    [Fact]
    public async Task Edit_EmptyReason_ValidatorRejects()
    {
        var validator = new EditCollateralMasterCommandValidator();
        var cmd = new EditCollateralMasterCommand(
            Guid.NewGuid(), null, "", null, null, null, null);
        var result = await validator.ValidateAsync(cmd);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Reason");
    }

    // ------------------------------------------------------------------
    // Test Admin-05: DELETE missing Reason — validator rejects
    // ------------------------------------------------------------------
    [Fact]
    public async Task SoftDelete_EmptyReason_ValidatorRejects()
    {
        var validator = new SoftDeleteCollateralMasterCommandValidator();
        var cmd = new SoftDeleteCollateralMasterCommand(Guid.NewGuid(), "");
        var result = await validator.ValidateAsync(cmd);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Reason");
    }

    // ------------------------------------------------------------------
    // Test Admin-06: Restore missing Reason — validator rejects
    // ------------------------------------------------------------------
    [Fact]
    public async Task Restore_EmptyReason_ValidatorRejects()
    {
        var validator = new RestoreCollateralMasterCommandValidator();
        var cmd = new RestoreCollateralMasterCommand(Guid.NewGuid(), "");
        var result = await validator.ValidateAsync(cmd);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Reason");
    }

    // ------------------------------------------------------------------
    // Test Admin-07: SoftDelete — sets IsDeleted=1; lookup no longer returns it;
    //                engagements still queryable by id; audit log written.
    //                Exercises domain + repository directly (bypasses HTTP auth).
    // ------------------------------------------------------------------
    [Fact]
    public async Task SoftDelete_SetsIsDeleted_LookupReturnsMiss_EngagementsPreserved()
    {
        var titleNo = $"ADMIN-07-{Guid.NewGuid():N}".Substring(0, 25);
        var masterId = await SeedLandMasterAsync("LO-A07", "Chiang Mai", "Mueang", "Suthep", titleNo);

        // Execute soft-delete via domain method directly
        using (var scope = CreateScope())
        {
            var repo = GetRepo(scope);
            var master = await repo.FindByIdAsync(masterId, TestContext.Current.CancellationToken);
            Assert.NotNull(master);
            master.SoftDelete("test deletion reason", "test-admin");
            await repo.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Verify IsDeleted=1 in DB
        using (var scope = CreateScope())
        {
            var db = GetCollateralDbContext(scope);
            var m = await db.CollateralMasters.FirstAsync(m => m.Id == masterId);
            Assert.True(m.IsDeleted);
        }

        // Verify lookup no longer returns it (lookup filters !IsDeleted)
        var lookupUrl = $"/collateral-masters/lookup?type=Land"
                        + $"&landOfficeCode=LO-A07"
                        + $"&province=Chiang+Mai"
                        + $"&district=Mueang"
                        + $"&subDistrict=Suthep"
                        + $"&titleType=Chanote"
                        + $"&titleNumber={titleNo}";
        var lookupResponse = await _client.GetAsync(lookupUrl);
        Assert.Equal(HttpStatusCode.NotFound, lookupResponse.StatusCode);

        // Verify engagements still queryable directly from DB
        using (var scope = CreateScope())
        {
            var db = GetCollateralDbContext(scope);
            var count = await db.CollateralEngagements
                .CountAsync(e => e.CollateralMasterId == masterId);
            Assert.True(count > 0, "Engagements must be preserved after soft-delete.");
        }

        // Verify audit log row was written via DispatchDomainEventInterceptor
        using (var scope = CreateScope())
        {
            var db = GetCollateralDbContext(scope);
            var auditLog = await db.CollateralMasterAuditLogs
                .FirstOrDefaultAsync(a => a.CollateralMasterId == masterId && a.Action == "SoftDelete");
            Assert.NotNull(auditLog);
            Assert.Equal("test deletion reason", auditLog.Reason);
        }
    }

    // ------------------------------------------------------------------
    // Test Admin-08: Restore — clears IsDeleted; lookup returns the master again;
    //                audit log has both SoftDelete and Restore entries.
    // ------------------------------------------------------------------
    [Fact]
    public async Task Restore_ClearsIsDeleted_LookupReturnsMasterAgain()
    {
        var titleNo = $"ADMIN-08-{Guid.NewGuid():N}".Substring(0, 25);
        var masterId = await SeedLandMasterAsync("LO-A08", "Phuket", "Mueang", "Talat Yai", titleNo);

        // Soft-delete
        using (var scope = CreateScope())
        {
            var repo = GetRepo(scope);
            var master = await repo.FindByIdAsync(masterId, TestContext.Current.CancellationToken);
            Assert.NotNull(master);
            master.SoftDelete("deletion for restore test", "test-admin");
            await repo.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Restore
        using (var scope = CreateScope())
        {
            var repo = GetRepo(scope);
            var master = await repo.FindByIdIncludingDeletedAsync(masterId, TestContext.Current.CancellationToken);
            Assert.NotNull(master);
            master.Restore("restored after test", "test-admin");
            await repo.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Verify IsDeleted cleared
        using (var scope = CreateScope())
        {
            var db = GetCollateralDbContext(scope);
            var m = await db.CollateralMasters.FirstAsync(m => m.Id == masterId);
            Assert.False(m.IsDeleted);
        }

        // Verify lookup returns the master
        var lookupUrl = $"/collateral-masters/lookup?type=Land"
                        + $"&landOfficeCode=LO-A08"
                        + $"&province=Phuket"
                        + $"&district=Mueang"
                        + $"&subDistrict=Talat+Yai"
                        + $"&titleType=Chanote"
                        + $"&titleNumber={titleNo}";
        var lookupResponse = await _client.GetAsync(lookupUrl);
        Assert.Equal(HttpStatusCode.OK, lookupResponse.StatusCode);

        // Verify audit log has both SoftDelete and Restore entries
        using (var scope = CreateScope())
        {
            var db = GetCollateralDbContext(scope);
            var logs = await db.CollateralMasterAuditLogs
                .Where(a => a.CollateralMasterId == masterId)
                .ToListAsync();
            Assert.Contains(logs, l => l.Action == "SoftDelete");
            Assert.Contains(logs, l => l.Action == "Restore");
        }
    }

    // ------------------------------------------------------------------
    // Test Admin-09: Edit — ownerName change persists + audit log row with field diff
    // ------------------------------------------------------------------
    [Fact]
    public async Task Edit_ValidFields_UpdatesPersisted_AuditLogWritten()
    {
        var titleNo = $"ADMIN-09-{Guid.NewGuid():N}".Substring(0, 25);
        var masterId = await SeedLandMasterAsync("LO-A09", "Khon Kaen", "Mueang", "Nai Mueang", titleNo);

        using (var scope = CreateScope())
        {
            var repo = GetRepo(scope);
            var master = await repo.FindByIdAsync(masterId, TestContext.Current.CancellationToken);
            Assert.NotNull(master);

            master.Edit(
                ownerName: "Updated Owner Name",
                land: new LandAdminEdit(
                    LandOfficeCode: null,
                    Province: null,
                    District: null,
                    SubDistrict: null,
                    TitleType: null,
                    TitleNumber: null,
                    SurveyNumber: null,
                    LandParcelNumber: null,
                    Rawang: null,
                    Street: "123 Test Street",
                    Village: null,
                    Latitude: null,
                    Longitude: null,
                    LandShapeType: null,
                    LandZoneType: null,
                    UrbanPlanningType: null,
                    AccessRoadWidth: null,
                    RoadFrontage: null,
                    LandArea: null),
                condo: null,
                leasehold: null,
                machine: null,
                reason: "Correct owner name",
                by: "test-admin");

            await repo.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Verify ownerName persisted
        using (var scope = CreateScope())
        {
            var db = GetCollateralDbContext(scope);
            var m = await db.CollateralMasters.FirstAsync(m => m.Id == masterId);
            Assert.Equal("Updated Owner Name", m.OwnerName);
        }

        // Verify audit log written with action=Edit and non-empty ChangedFields
        using (var scope = CreateScope())
        {
            var db = GetCollateralDbContext(scope);
            var log = await db.CollateralMasterAuditLogs
                .FirstOrDefaultAsync(a => a.CollateralMasterId == masterId && a.Action == "Edit");
            Assert.NotNull(log);
            Assert.Equal("Correct owner name", log.Reason);
            Assert.NotEmpty(log.ChangedFields ?? "");
            Assert.Contains("OwnerName", log.ChangedFields!);
        }
    }

    // ------------------------------------------------------------------
    // Test Admin-10: SoftDelete RESTRICT — blocked by active Leasehold references.
    //                This is test 19 from step 3 verification plan.
    // ------------------------------------------------------------------
    [Fact]
    public async Task SoftDelete_BlockedByActiveLeaseholdReference_DetectedCorrectly()
    {
        // Create an underlying Land master
        var landTitleNo = $"ADMIN-10L-{Guid.NewGuid():N}".Substring(0, 25);
        var landMasterId = await SeedLandMasterAsync("LO-A10", "Bangkok", "Bangrak", "Silom", landTitleNo);

        // Create a Leasehold master over it
        Guid leaseholdMasterId;
        using (var scope = CreateScope())
        {
            var db = GetCollateralDbContext(scope);
            var leasehold = CollateralMaster.CreateLeasehold(
                lessee: "Test Lessee",
                leaseRegistrationNo: $"LH-{Guid.NewGuid():N}".Substring(0, 20),
                underlyingMasterId: landMasterId,
                lessor: "Test Lessor",
                leaseTermStart: new DateOnly(2025, 1, 1));
            db.CollateralMasters.Add(leasehold);
            await db.SaveChangesAsync();
            leaseholdMasterId = leasehold.Id;
        }

        // Verify the RESTRICT check via repository method
        using (var scope = CreateScope())
        {
            var repo = GetRepo(scope);
            var activeLeaseholds = await repo.GetActiveLeaseholdIdsForUnderlyingAsync(
                landMasterId, TestContext.Current.CancellationToken);

            Assert.Contains(leaseholdMasterId, activeLeaseholds);
            Assert.True(activeLeaseholds.Count > 0,
                "RESTRICT: GetActiveLeaseholdIdsForUnderlyingAsync must detect the active leasehold.");
        }
    }

    // ------------------------------------------------------------------
    // Test Admin-11: Restore dedup-collision — detected correctly
    // ------------------------------------------------------------------
    [Fact]
    public async Task Restore_DedupKeyCollision_Detected()
    {
        var titleNo = $"ADMIN-11-{Guid.NewGuid():N}".Substring(0, 25);

        // Create and soft-delete first master
        var masterId = await SeedLandMasterAsync("LO-A11", "Bangkok", "Bangrak", "Silom", titleNo);
        using (var scope = CreateScope())
        {
            var repo = GetRepo(scope);
            var master = await repo.FindByIdAsync(masterId, TestContext.Current.CancellationToken);
            Assert.NotNull(master);
            master.SoftDelete("delete for collision test", "test-admin");
            await repo.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Create a second master with the SAME dedup key
        await SeedLandMasterAsync("LO-A11", "Bangkok", "Bangrak", "Silom", titleNo);

        // Verify that LandDedupCollidesAsync detects the collision
        using (var scope = CreateScope())
        {
            var repo = GetRepo(scope);
            var master = await repo.FindByIdIncludingDeletedAsync(masterId, TestContext.Current.CancellationToken);
            Assert.NotNull(master);
            var ld = master.LandDetail!;

            var collides = await repo.LandDedupCollidesAsync(
                master.Id,
                ld.Province, ld.District, ld.SubDistrict,
                ld.TitleType, ld.TitleNumber, ld.SurveyNumber, ld.LandParcelNumber, ld.Rawang,
                TestContext.Current.CancellationToken);

            Assert.True(collides,
                "Should detect that a second active master exists with the same dedup key, blocking restore.");
        }
    }

    // ------------------------------------------------------------------
    // Test Admin-12: Edit dedup-collision — detected correctly
    // ------------------------------------------------------------------
    [Fact]
    public async Task Edit_DedupKeyCollision_Detected()
    {
        var titleNoA = $"ADMIN-12A-{Guid.NewGuid():N}".Substring(0, 25);
        var titleNoB = $"ADMIN-12B-{Guid.NewGuid():N}".Substring(0, 25);

        // Create two separate masters
        var masterIdA = await SeedLandMasterAsync("LO-A12", "Bangkok", "Bangrak", "Silom", titleNoA);
        await SeedLandMasterAsync("LO-A12", "Bangkok", "Bangrak", "Silom", titleNoB);

        // Attempt to change master A's TitleDeedNo to match master B — should collide
        using (var scope = CreateScope())
        {
            var repo = GetRepo(scope);
            var masterA = await repo.FindByIdAsync(masterIdA, TestContext.Current.CancellationToken);
            Assert.NotNull(masterA);
            var ld = masterA.LandDetail!;

            var collides = await repo.LandDedupCollidesAsync(
                masterIdA,
                ld.Province, ld.District, ld.SubDistrict,
                ld.TitleType,
                titleNoB,   // would match master B
                ld.SurveyNumber, ld.LandParcelNumber, ld.Rawang,
                TestContext.Current.CancellationToken);

            Assert.True(collides,
                "Changing TitleNumber to match another active master should be detected as a collision.");
        }
    }
}
