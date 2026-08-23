using System.Text.Json;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// Proves the data-correction audit trail is atomic: AppraisalPropertyCorrectionAuditLogWriter only
/// calls Add() and relies on DispatchDomainEventInterceptor to commit the audit row inside the same
/// transaction as the correction itself.
///
/// A unit test cannot cover this — the guarantee lives in the interceptor + SaveChanges pipeline. If
/// someone "fixes" the writer by giving it its own DbContext or calling SaveChanges, the correction
/// and its audit row could diverge, and the whole point of the feature (an attributable edit) is
/// lost. These tests fail in that case.
/// </summary>
[Collection("Integration")]
public class PropertyCorrectionAuditTests(IntegrationTestFixture fixture)
{
    private IServiceScope CreateScope()
        => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    private static LandCorrection EmptyLand() => new(
        PropertyName: null, LandDescription: null, Latitude: null, Longitude: null,
        SubDistrict: null, District: null, Province: null, LandOffice: null,
        DopaSubDistrict: null, DopaDistrict: null, DopaProvince: null,
        OwnerName: null, IsOwnerVerified: null, HasObligation: null, ObligationDetails: null,
        IsLandLocationVerified: null, LandCheckMethodType: null, LandCheckMethodTypeOther: null,
        Street: null, Soi: null, Village: null, AddressLocation: null, Remark: null);

    private static PropertyCorrectionData LandOnly(LandCorrection land) =>
        new(null, land, null, null, null, null, null, null, null);

    /// <summary>Persists a land appraisal with one property and returns (appraisalId, propertyId).</summary>
    private async Task<(Guid AppraisalId, Guid PropertyId)> SeedLandAppraisalAsync(CancellationToken ct)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
        appraisal.SetAppraisalNumber($"COR-{Guid.NewGuid():N}"[..18]);

        var property = appraisal.AddLandProperty();
        property.LandDetail!.Update(
            propertyName: "Seed Plot",
            ownerName: "Owner A",
            address: Address.Create("ท่าทราย", "เมืองสมุทรสาคร", "สมุทรสาคร"),
            street: "พระราม 2",
            landOffice: "สำนักงานที่ดินสมุทรสาคร");

        db.Appraisals.Add(appraisal);
        await db.SaveChangesAsync(ct);

        return (appraisal.Id, property.Id);
    }

    [Fact]
    public async Task Correction_writes_the_audit_row_in_the_same_transaction()
    {
        var ct = TestContext.Current.CancellationToken;
        var (appraisalId, propertyId) = await SeedLandAppraisalAsync(ct);

        // ── Act: correct the owner name through the domain, then save once. ──
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

            var appraisal = await db.Appraisals
                .Include(a => a.Properties)
                .FirstAsync(a => a.Id == appraisalId, ct);

            var outcome = appraisal.CorrectPropertyData(
                propertyId,
                LandOnly(EmptyLand() with { OwnerName = "Owner B" }),
                "owner keyed from the contact person",
                "EMP001");

            Assert.Equal(1, outcome.ChangedFieldCount);

            await db.SaveChangesAsync(ct);
        }

        // ── Assert: a fresh scope sees BOTH the new value and its audit row. ──
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

            var appraisal = await db.Appraisals
                .AsNoTracking()
                .Include(a => a.Properties)
                .FirstAsync(a => a.Id == appraisalId, ct);

            Assert.Equal("Owner B", appraisal.Properties.Single().LandDetail!.OwnerName);

            var log = await db.AppraisalPropertyCorrectionLogs
                .AsNoTracking()
                .SingleAsync(l => l.AppraisalId == appraisalId, ct);

            Assert.Equal(propertyId, log.AppraisalPropertyId);
            Assert.Equal(PropertyType.Land.Code, log.PropertyType);
            Assert.Equal("owner keyed from the contact person", log.Reason);
            Assert.Equal("EMP001", log.ChangedBy);
            Assert.NotEqual(default, log.ChangedAt);

            using var document = JsonDocument.Parse(log.ChangedFields);
            var change = document.RootElement.GetProperty("Land.OwnerName");
            Assert.Equal("Owner A", change.GetProperty("from").GetString());
            Assert.Equal("Owner B", change.GetProperty("to").GetString());
        }
    }

    [Fact]
    public async Task Correction_persists_only_the_corrected_field()
    {
        var ct = TestContext.Current.CancellationToken;
        var (appraisalId, propertyId) = await SeedLandAppraisalAsync(ct);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

            var appraisal = await db.Appraisals
                .Include(a => a.Properties)
                .FirstAsync(a => a.Id == appraisalId, ct);

            appraisal.CorrectPropertyData(
                propertyId,
                LandOnly(EmptyLand() with { Province = "สมุทรปราการ" }),
                "wrong province",
                "EMP001");

            await db.SaveChangesAsync(ct);
        }

        // The failure mode this guards against is a partial correction round-tripping through
        // LandAppraisalDetail.Update and nulling every column it did not mention.
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

            var landDetail = (await db.Appraisals
                .AsNoTracking()
                .Include(a => a.Properties)
                .FirstAsync(a => a.Id == appraisalId, ct))
                .Properties.Single().LandDetail!;

            Assert.Equal("สมุทรปราการ", landDetail.Address!.Province);
            Assert.Equal("ท่าทราย", landDetail.Address.SubDistrict);
            Assert.Equal("เมืองสมุทรสาคร", landDetail.Address.District);
            Assert.Equal("Seed Plot", landDetail.PropertyName);
            Assert.Equal("Owner A", landDetail.OwnerName);
            Assert.Equal("พระราม 2", landDetail.Street);
            Assert.Equal("สำนักงานที่ดินสมุทรสาคร", landDetail.LandOffice);
        }
    }

    [Fact]
    public async Task No_change_writes_no_audit_row()
    {
        var ct = TestContext.Current.CancellationToken;
        var (appraisalId, propertyId) = await SeedLandAppraisalAsync(ct);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

            var appraisal = await db.Appraisals
                .Include(a => a.Properties)
                .FirstAsync(a => a.Id == appraisalId, ct);

            var outcome = appraisal.CorrectPropertyData(
                propertyId,
                LandOnly(EmptyLand() with { OwnerName = "Owner A" }), // already the stored value
                "no-op",
                "EMP001");

            Assert.Equal(0, outcome.ChangedFieldCount);

            await db.SaveChangesAsync(ct);
        }

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

            var logCount = await db.AppraisalPropertyCorrectionLogs
                .AsNoTracking()
                .CountAsync(l => l.AppraisalId == appraisalId, ct);

            Assert.Equal(0, logCount);
        }
    }
}
