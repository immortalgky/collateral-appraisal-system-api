using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Collateral.CollateralMasters.Services;
using Collateral.Data;
using Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;
using Address = Appraisal.Domain.Appraisals.Address;

namespace Integration.Collateral.Integration.Tests;

/// <summary>
/// The chain fallback: when a collateral's dedup key finds nothing, reuse the master of the
/// appraisal named by <c>PrevAppraisalId</c>.
///
/// The fallback used to admit only AppraisalType 'ReAppraisal' and 'Progressive'. Every other
/// appraisal carrying a PrevAppraisalId — a New follow-up, an appeal — fell through and minted a
/// second master for collateral we already held, silently splitting its history. Business decision:
/// a PrevAppraisalId is evidence enough, so the type test is gone.
///
/// These tests pin both halves of that: the type no longer blocks the fallback, and the guards that
/// remain still do their job.
/// </summary>
[Collection("Integration")]
public class ChainFallbackAppraisalTypeTests(IntegrationTestFixture fixture)
{
    private IServiceScope CreateScope()
        => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    /// <param name="appraisalType">
    /// 'New' in every test here — the point is that it is NOT one of the two types the old gate
    /// allowed.
    /// </param>
    private static AppraisalAggregate CreateAppraisal(string appraisalType, Guid? prevAppraisalId)
    {
        var a = AppraisalAggregate.Create(
            Guid.NewGuid(), appraisalType, "Normal", DateTime.Now, prevAppraisalId: prevAppraisalId);
        a.SetAppraisalNumber($"AP-{Guid.NewGuid():N}"[..18]);
        // CompletedAt is set directly: the collateral write path only runs for completed appraisals,
        // and driving the real status transitions here would test the Appraisal module instead.
        typeof(AppraisalAggregate).GetProperty("CompletedAt")!.SetValue(a, DateTime.UtcNow);
        return a;
    }

    /// <summary>Seeds one completed land appraisal and runs the collateral write path.</summary>
    private async Task<Guid> SeedAndProcessAsync(
        string titleNo, string province, string district, string subDistrict,
        string appraisalType = "New", Guid? prevAppraisalId = null)
    {
        Guid appraisalId;

        using (var seedScope = CreateScope())
        {
            var appraisalDb = seedScope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var a = CreateAppraisal(appraisalType, prevAppraisalId);

            var prop = a.AddLandProperty();
            prop.LandDetail!.Update(
                address: Address.Create(subDistrict, district, province), landOffice: "LO-CHAIN");
            prop.LandDetail.AddTitle(LandTitle.Create(prop.LandDetail.Id, titleNo, "Chanote"));

            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;
        }

        // Fresh scope, mirroring the consumer: each message gets its own DbContext.
        using var runScope = CreateScope();
        await runScope.ServiceProvider.GetRequiredService<ICollateralMasterUpsertService>()
            .ProcessAppraisalAsync(appraisalId, TestContext.Current.CancellationToken);

        return appraisalId;
    }

    /// <summary>The master an appraisal ended up bound to, via its engagement.</summary>
    private async Task<Guid?> MasterIdOfAsync(Guid appraisalId)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollateralDbContext>();
        return await db.CollateralEngagements.AsNoTracking()
            .Where(e => e.AppraisalId == appraisalId)
            .Select(e => (Guid?)e.CollateralMasterId)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The case the removed gate used to block: a 'New' appraisal that follows another one, whose
    /// title number picked up a typo along the way. The dedup key misses, and before this change the
    /// type test stopped the fallback dead — a second master for the same parcel, with the history
    /// split across the two and no merge tool to put it back.
    /// </summary>
    [Fact]
    public async Task NewAppraisalWithPrevAppraisalId_DriftedTitle_ReusesThePreviousMaster()
    {
        var titleNo = $"CHAIN-{Guid.NewGuid():N}"[..16];

        var firstId = await SeedAndProcessAsync(titleNo, "BKK", "D-CHAIN", "S-CHAIN");
        var firstMasterId = await MasterIdOfAsync(firstId);
        Assert.NotNull(firstMasterId);

        // Same parcel, same location — only the title number drifted, so the dedup key cannot match.
        var secondId = await SeedAndProcessAsync(
            titleNo + "9", "BKK", "D-CHAIN", "S-CHAIN",
            appraisalType: "New", prevAppraisalId: firstId);

        Assert.Equal(firstMasterId, await MasterIdOfAsync(secondId));
    }

    /// <summary>
    /// A different sub-district on the deed no longer refuses the fallback.
    ///
    /// This test used to assert the opposite: LandLocationMatches required the deed's province +
    /// district + sub-district to match exactly, so a drifted title AND a different sub-district
    /// minted a second master. That gate was removed on 2026-08-18 — the deed address follows the
    /// land office's division of the country, which is re-cut over time and carries its former name
    /// in brackets, so the same parcel legitimately reads differently across two appraisals. On U3 it
    /// was splitting 1,475 chains, of which 1,135 sat within 200 m of each other.
    ///
    /// The governing principle: PrevAppraisalId is the USER's assertion that this appraisal follows
    /// that one, and the system does not overrule it on evidence this weak. What still guards the
    /// path is compatibleTypes, alreadyClaimed and the IsMaster test — see the tests below.
    /// </summary>
    [Fact]
    public async Task NewAppraisalWithPrevAppraisalId_DifferentSubDistrict_StillReuses()
    {
        var titleNo = $"CHAIN-{Guid.NewGuid():N}"[..16];

        var firstId = await SeedAndProcessAsync(titleNo, "BKK", "D-CHAIN", "S-ORIGINAL");
        var firstMasterId = await MasterIdOfAsync(firstId);
        Assert.NotNull(firstMasterId);

        // Both the title number and the sub-district drifted — the case the old guard refused.
        var secondId = await SeedAndProcessAsync(
            titleNo + "9", "BKK", "D-CHAIN", "S-ELSEWHERE",
            appraisalType: "New", prevAppraisalId: firstId);

        Assert.Equal(firstMasterId, await MasterIdOfAsync(secondId));
    }

    /// <summary>
    /// Dropping the location gate must not have dropped the type gate with it. The previous appraisal
    /// is bound to a CONDO master; this appraisal values land. compatibleTypes has to refuse that —
    /// binding across collateral families is the one thing PrevAppraisalId can never justify.
    /// </summary>
    [Fact]
    public async Task PreviousAppraisalIsACondo_LandAppraisalDoesNotReuseIt()
    {
        Guid condoAppraisalId;
        using (var seedScope = CreateScope())
        {
            var appraisalDb = seedScope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var a = CreateAppraisal("New", prevAppraisalId: null);
            var prop = a.AddCondoProperty();
            prop.CondoDetail!.Update(
                condoRegistrationNumber: $"REG-{Guid.NewGuid():N}"[..12],
                buildingNumber: "A",
                floorNumber: "12",
                roomNumber: "1201",
                ownerName: "Condo Owner",
                address: Address.Create("S-CONDO", "D-CONDO", "BKK"));

            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            condoAppraisalId = a.Id;
        }

        using (var runScope = CreateScope())
        {
            await runScope.ServiceProvider.GetRequiredService<ICollateralMasterUpsertService>()
                .ProcessAppraisalAsync(condoAppraisalId, TestContext.Current.CancellationToken);
        }

        var condoMasterId = await MasterIdOfAsync(condoAppraisalId);
        Assert.NotNull(condoMasterId);

        // A land appraisal that names the condo appraisal as its predecessor.
        var landId = await SeedAndProcessAsync(
            $"CHAIN-{Guid.NewGuid():N}"[..16], "BKK", "D-CHAIN", "S-CHAIN",
            appraisalType: "New", prevAppraisalId: condoAppraisalId);

        var landMasterId = await MasterIdOfAsync(landId);
        Assert.NotNull(landMasterId);
        Assert.NotEqual(condoMasterId, landMasterId);
    }

    /// <summary>
    /// The fallback is the second resort, not the first: when the dedup key matches, it decides.
    /// A regression here would mean an appraisal binding to whatever its predecessor happened to
    /// touch instead of to the collateral it actually values.
    /// </summary>
    [Fact]
    public async Task DedupKeyHit_WinsOverThePreviousAppraisal()
    {
        var titleA = $"CHAIN-A-{Guid.NewGuid():N}"[..18];
        var titleB = $"CHAIN-B-{Guid.NewGuid():N}"[..18];

        // Two unrelated parcels, each with its own master.
        var aId = await SeedAndProcessAsync(titleA, "BKK", "D-CHAIN", "S-HIT");
        var bId = await SeedAndProcessAsync(titleB, "BKK", "D-CHAIN", "S-HIT");
        var aMasterId = await MasterIdOfAsync(aId);
        var bMasterId = await MasterIdOfAsync(bId);
        Assert.NotEqual(aMasterId, bMasterId);

        // Re-values parcel B while naming A as its predecessor. The dedup key finds B, so B wins.
        var againId = await SeedAndProcessAsync(
            titleB, "BKK", "D-CHAIN", "S-HIT", appraisalType: "New", prevAppraisalId: aId);

        Assert.Equal(bMasterId, await MasterIdOfAsync(againId));
    }
}
