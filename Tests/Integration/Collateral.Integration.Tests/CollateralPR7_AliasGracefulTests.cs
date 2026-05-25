using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Collateral.CollateralMasters.Models;
using Collateral.CollateralMasters.Services;
using Collateral.Data;
using Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Collateral.Integration.Tests;

/// <summary>
/// PR-7 integration tests: alias-alone graceful behavior.
///
/// The alias-alone validation was moved upstream to the Request module.
/// The Collateral module is now a trusting consumer: when a property's existing
/// master row is an alias, the service resolves to the parent IsMaster and
/// attaches the engagement there without throwing.
///
/// Test inventory:
///   PR7-1  Condo alias alone → service succeeds, engagement anchored on parent IsMaster,
///          parent IsMaster classification unchanged.
/// </summary>
[Collection("Integration")]
public class CollateralPR7_AliasGracefulTests(IntegrationTestFixture fixture)
{
    // -----------------------------------------------------------------------
    // Seed helpers
    // -----------------------------------------------------------------------

    private static AppraisalAggregate CreateAppraisalSeed(Guid requestId, string prefix = "PR7")
    {
        var a = AppraisalAggregate.Create(requestId, "New", "Normal", DateTime.Now);
        a.SetAppraisalNumber($"AP-{prefix}-{Guid.NewGuid():N}"[..18]);
        typeof(AppraisalAggregate)
            .GetProperty("CompletedAt")!
            .SetValue(a, DateTime.UtcNow);
        return a;
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
    // PR7-1: Condo alias alone → graceful re-anchor on parent IsMaster
    //
    // Setup:
    //   - Seed a Condo master (IsMaster=true) via reflection as IsMaster=false with
    //     a parent pointing at a separately seeded IsMaster row.
    //   - Submit an appraisal targeting the alias unit's dedup key alone.
    //
    // Assert:
    //   - ProcessAppraisalAsync completes without exception.
    //   - One engagement created, anchored to the parent IsMaster.
    //   - Parent IsMaster classification (IsMaster=true, ParentMasterId=null) unchanged.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task PR7_1_CondoAliasAlone_GracefullyAttachesEngagementToParentIsMaster()
    {
        var regNo = "CONDO-PR7-" + Guid.NewGuid().ToString("N")[..6];
        const string building = "A";
        const string floor = "10";
        const string room1 = "1001"; // will become IsMaster
        const string room2 = "1002"; // will be manually demoted to alias
        const string titleNo1 = "PR7-T1";
        const string titleNo2 = "PR7-T2";
        const string titleType = "Chanote";
        const string province = "Bangkok";
        const string landOffice = "LO-PR7";
        var ct = TestContext.Current.CancellationToken;

        // ---- Seed two Condo master rows directly in the DB ----
        // We create master (IsMaster=true) and alias (IsMaster=false, ParentMasterId=master.Id)
        // manually because there is no CreateCondoAlias factory. This simulates the state
        // that would exist after a prior admin re-grouping or future condo-group feature.
        Guid parentMasterId;
        Guid aliasMasterId;
        using (var seed = CreateScope())
        {
            var db = GetCollateralDbContext(seed);

            // Create the IsMaster row (room 1001)
            var parentMaster = CollateralMaster.CreateCondo(
                ownerName: "Owner A",
                landOfficeCode: landOffice,
                condoRegistrationNumber: regNo,
                buildingNumber: building,
                floorNumber: floor,
                roomNumber: room1,
                titleNumber: titleNo1,
                titleType: titleType,
                condoName: "Test Condo",
                province: province);
            db.CollateralMasters.Add(parentMaster);
            await db.SaveChangesAsync(ct);
            parentMasterId = parentMaster.Id;

            // Create a second Condo master (initially IsMaster=true) then demote it to alias
            // via reflection (no CreateCondoAlias factory exists for Condo).
            var aliasMaster = CollateralMaster.CreateCondo(
                ownerName: "Owner B",
                landOfficeCode: landOffice,
                condoRegistrationNumber: regNo,
                buildingNumber: building,
                floorNumber: floor,
                roomNumber: room2,
                titleNumber: titleNo2,
                titleType: titleType,
                condoName: "Test Condo",
                province: province);

            // Demote to alias: set IsMaster=false and ParentMasterId=parentMasterId
            typeof(CollateralMaster)
                .GetProperty("IsMaster")!
                .SetValue(aliasMaster, false);
            typeof(CollateralMaster)
                .GetProperty("ParentMasterId")!
                .SetValue(aliasMaster, parentMasterId);

            db.CollateralMasters.Add(aliasMaster);
            await db.SaveChangesAsync(ct);
            aliasMasterId = aliasMaster.Id;
        }

        // ---- Verify the alias is set up correctly ----
        using (var verify = CreateScope())
        {
            var db = GetCollateralDbContext(verify);
            var alias = await db.CollateralMasters.FindAsync([aliasMasterId], ct);
            Assert.NotNull(alias);
            Assert.False(alias.IsMaster, "Pre-condition: alias must be IsMaster=false");
            Assert.Equal(parentMasterId, alias.ParentMasterId);
        }

        // ---- Submit an appraisal targeting only the alias unit (room2 / titleNo2) ----
        Guid appraisalId;
        using (var seed = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seed);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            SeedCondoProperty(a, landOffice, regNo, building, floor, room2, titleNo2, titleType, province);
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(ct);
            appraisalId = a.Id;
        }

        // ---- Act: must NOT throw ----
        await ProcessAppraisalInNewScopeAsync(appraisalId);

        // ---- Assert ----
        using var assert = CreateScope();
        var assertDb = GetCollateralDbContext(assert);

        // One engagement created for the appraisal
        var engagements = await assertDb.CollateralEngagements
            .Where(e => e.AppraisalId == appraisalId)
            .ToListAsync(ct);

        Assert.Single(engagements);

        // Engagement anchored to the parent IsMaster, not the alias
        Assert.Equal(parentMasterId, engagements.Single().CollateralMasterId);

        // Parent IsMaster classification is unchanged
        var parentAfter = await assertDb.CollateralMasters
            .FirstAsync(m => m.Id == parentMasterId, ct);

        Assert.True(parentAfter.IsMaster, "Parent IsMaster must remain IsMaster=true");
        Assert.Null(parentAfter.ParentMasterId);
    }
}
