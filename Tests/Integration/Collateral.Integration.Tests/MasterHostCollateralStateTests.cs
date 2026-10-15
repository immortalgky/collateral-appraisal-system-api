using Collateral.Contracts;
using Collateral.Contracts.HostLink;
using Collateral.Data;
using Integration.Contracts.HostLink;
using Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CollateralMasterEntity = Collateral.CollateralMasters.Models.CollateralMaster;

namespace Integration.Collateral.Integration.Tests;

/// <summary>
/// AS400 host state on the CollateralMaster: ingesting it and propagating it to the group's alias rows.
/// The outbound COLLATERAL_RESULT no longer reads the master (vw_CollateralResultExport walks the
/// appraisal chain instead), so its grain is not tested here.
///
/// The state used to live on CollateralEngagement, one row per appraisal. It moved because AS400 keys
/// collateral, not appraisals — it mints one id per collateral at drawdown and reports redemption
/// against that same id, with no notion of which appraisal is involved.
/// </summary>
[Collection("Integration")]
public class MasterHostCollateralStateTests(IntegrationTestFixture fixture)
{
    private IServiceScope CreateScope()
        => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    private static ParsedHostLinkRecord Record(
        string appraisalNumber, string hostId, string indicator, DateOnly date)
        => new(appraisalNumber, hostId, CollateralName: null, Address1: null, date, indicator,
            LocationCode: null, CollateralCode: null, PropertyType: null, PropertyTypeDesc: null,
            MasterTitle: "Y", RowHash: $"{appraisalNumber}:{hostId}:{indicator}");

    private static Task<HostLinkIngestResult> IngestAsync(
        IServiceScope scope, params ParsedHostLinkRecord[] records)
        => scope.ServiceProvider.GetRequiredService<IHostCollateralLinkIngestor>()
            .IngestAsync(
                "AS400_COLLATLINK_20260601.txt",
                new DateOnly(2026, 6, 1),
                new ParsedHostLinkFile(new DateOnly(2026, 6, 1), [.. records]));

    private static string NewTitle() => $"HS-{Guid.NewGuid():N}"[..14];

    /// <summary>Land master with one engagement, plus <paramref name="aliasCount"/> extra titles.</summary>
    private static async Task<(Guid MasterId, string AppraisalNumber, Guid AppraisalId)>
        SeedGroupAsync(CollateralDbContext db, int aliasCount = 0, DateTime? appraisalDate = null,
                       string? appraisalNumber = null)
    {
        var master = CollateralMasterEntity.CreateLand(
            ownerName: "Test Owner",
            landOfficeCode: "0100",
            province: "10", district: "1001", subDistrict: "100101",
            titleType: "NS4", titleNumber: NewTitle(),
            surveyNumber: null, landParcelNumber: null, rawang: null,
            street: null, village: null, latitude: null, longitude: null);

        var number = appraisalNumber ?? $"AP-HS-{Guid.NewGuid():N}"[..16];
        var appraisalId = Guid.CreateVersion7();

        master.AppendEngagement(
            appraisalId: appraisalId,
            appraisalNumber: number,
            requestId: Guid.CreateVersion7(),
            requestNumber: "RQ-HS",
            appraisalType: "New",
            appraisalDate: appraisalDate ?? DateTime.Now,
            appraiserUserId: "tester",
            appraisalCompanyId: null,
            appraisalCompanyName: null,
            constructionInspectionFeeAmount: null,
            snapshot: "{}",
            createdAt: DateTime.Now,
            appraisedCollateralType: CollateralTypes.Land);

        db.CollateralMasters.Add(master);

        for (var i = 0; i < aliasCount; i++)
        {
            db.CollateralMasters.Add(CollateralMasterEntity.CreateLandAlias(
                parentMasterId: master.Id,
                landOfficeCode: "0100",
                province: "10", district: "1001", subDistrict: "100101",
                titleType: "NS4", titleNumber: NewTitle(),
                surveyNumber: null, landParcelNumber: null, rawang: null));
        }

        await db.SaveChangesAsync();
        return (master.Id, number, appraisalId);
    }

    private static async Task<CollateralMasterEntity> ReloadAsync(CollateralDbContext db, Guid masterId)
        => await db.CollateralMasters.AsNoTracking().SingleAsync(m => m.Id == masterId);

    private static async Task<List<CollateralMasterEntity>> ReloadAliasesAsync(
        CollateralDbContext db, Guid masterId)
        => await db.CollateralMasters.AsNoTracking()
            .Where(m => m.ParentMasterId == masterId).ToListAsync();

    // ── Redemption reaches the whole group ────────────────────────────────────────────────────

    /// <summary>
    /// A redemption releases every title in the physical group, but only the IsMaster row holds an
    /// engagement, so nothing in the ingest loop reaches the aliases on its own. Left unflagged they
    /// would keep being reported to the regulator as still held.
    /// </summary>
    [Fact]
    public async Task Redemption_FlagsTheMasterAndEveryAlias()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollateralDbContext>();

        var (masterId, appraisalNumber, _) = await SeedGroupAsync(db, aliasCount: 2);

        await IngestAsync(scope, Record(
            appraisalNumber, "77001", HostLinkRecordIndicators.Redeemed, new DateOnly(2026, 5, 31)));

        var master = await ReloadAsync(db, masterId);
        Assert.True(master.IsRedeemed);
        Assert.Equal(new DateOnly(2026, 5, 31), master.RedeemedDate);
        // The id is kept, not cleared: the regulator's file names the collateral that was released.
        Assert.Equal("77001", master.HostCollateralId);

        var aliases = await ReloadAliasesAsync(db, masterId);
        Assert.Equal(2, aliases.Count);
        Assert.All(aliases, a =>
        {
            Assert.True(a.IsRedeemed);
            Assert.Equal(new DateOnly(2026, 5, 31), a.RedeemedDate);
            // AS400 issued one id for the group; duplicating it across rows would break lookup by it.
            Assert.Null(a.HostCollateralId);
        });
    }

    /// <summary>
    /// Re-pledge after a release. Without clearing the flag the collateral stays filtered out of the
    /// regulatory export permanently while the bank actually holds it again — and the aliases have to
    /// come back too, or the group's other titles stay marked released.
    /// </summary>
    [Fact]
    public async Task DrawdownAfterRedemption_ClearsTheFlagAcrossTheGroup()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollateralDbContext>();

        var (masterId, appraisalNumber, _) = await SeedGroupAsync(db, aliasCount: 1);

        await IngestAsync(scope, Record(
            appraisalNumber, "77002", HostLinkRecordIndicators.Redeemed, new DateOnly(2025, 5, 5)));
        Assert.True((await ReloadAsync(db, masterId)).IsRedeemed);

        // A later facility against the same collateral: AS400 issues a fresh id.
        await IngestAsync(scope, Record(
            appraisalNumber, "77003", HostLinkRecordIndicators.Drawdown, new DateOnly(2026, 7, 7)));

        var master = await ReloadAsync(db, masterId);
        Assert.False(master.IsRedeemed);
        Assert.Null(master.RedeemedDate);
        Assert.Equal("77003", master.HostCollateralId);

        Assert.All(await ReloadAliasesAsync(db, masterId), a =>
        {
            Assert.False(a.IsRedeemed);
            Assert.Null(a.RedeemedDate);
        });
    }

    // ── Ordering ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Within one file a redemption wins, wherever it sits. RecordDate cannot order the rows — it is
    /// the transmit date, identical on every row of a file — and losing a redemption reports exposure
    /// the bank no longer has. Here the redemption comes FIRST, so taking the last row would drop it.
    /// </summary>
    [Fact]
    public async Task WithinOneFile_RedemptionWinsRegardlessOfRowOrder()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollateralDbContext>();

        var (masterId, appraisalNumber, _) = await SeedGroupAsync(db);

        await IngestAsync(scope,
            Record(appraisalNumber, "77004", HostLinkRecordIndicators.Redeemed, new DateOnly(2026, 6, 1)),
            Record(appraisalNumber, "77004", HostLinkRecordIndicators.Drawdown, new DateOnly(2026, 6, 1)));

        var master = await ReloadAsync(db, masterId);
        Assert.True(master.IsRedeemed);
        Assert.Equal("77004", master.HostCollateralId);
    }

    /// <summary>Re-ingesting the same row writes nothing and is reported as unchanged, not updated.</summary>
    [Fact]
    public async Task ReIngestingTheSameRow_IsReportedUnchanged()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollateralDbContext>();

        var (_, appraisalNumber, _) = await SeedGroupAsync(db, aliasCount: 1);
        var row = Record(appraisalNumber, "77005", HostLinkRecordIndicators.Drawdown, new DateOnly(2026, 3, 3));

        var first = await IngestAsync(scope, row);
        var second = await IngestAsync(scope, row);

        Assert.Equal(1, first.Updated);
        Assert.Equal(1, second.Unchanged);
        Assert.Equal(0, second.Updated);
    }
}
