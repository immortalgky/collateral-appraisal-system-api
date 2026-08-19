using Collateral.Contracts;
using Collateral.Contracts.FileInterface;
using Collateral.Contracts.HostLink;
using Collateral.Data;
using Integration.Contracts.HostLink;
using Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CollateralMasterEntity = Collateral.CollateralMasters.Models.CollateralMaster;

namespace Integration.Collateral.Integration.Tests;

/// <summary>
/// AS400 host state on the CollateralMaster: ingesting it, propagating it to the group's alias rows,
/// and the effect on the outbound COLLATERAL_RESULT.
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
        => new(appraisalNumber, hostId, date, indicator, RowHash: $"{appraisalNumber}:{hostId}:{indicator}");

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
    /// AS400 orders its file by collateral id, not by event date, so file position says nothing about
    /// which event is the more recent. Here the redemption is the LAST row but the OLDER event; taking
    /// the last one would release collateral the bank still holds.
    /// </summary>
    [Fact]
    public async Task WithinOneFile_TheLatestEventDateWinsRegardlessOfRowOrder()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollateralDbContext>();

        var (masterId, appraisalNumber, _) = await SeedGroupAsync(db);

        await IngestAsync(scope,
            Record(appraisalNumber, "77004", HostLinkRecordIndicators.Drawdown, new DateOnly(2026, 7, 7)),
            Record(appraisalNumber, "77004", HostLinkRecordIndicators.Redeemed, new DateOnly(2025, 5, 5)));

        var master = await ReloadAsync(db, masterId);
        Assert.False(master.IsRedeemed);
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

    // ── Outbound file grain ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// The outbound file carries one row per collateral, not per appraisal.
    ///
    /// This is the reason moving the id could not be done on its own: `HostCollateralId IS NOT NULL`
    /// is the gate deciding which rows are ready to send. Keyed to the master while still emitting one
    /// row per appraisal, every never-sent older appraisal of that master would go out at once, each
    /// stamped with the master's single id.
    /// </summary>
    [Fact]
    public async Task OutboundFile_EmitsOneRowPerMaster_UsingTheLatestEngagement()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollateralDbContext>();

        var (masterId, appraisalNumber, _) = await SeedGroupAsync(
            db, appraisalDate: new DateTime(2024, 1, 1));

        // Two later appraisals of the same collateral.
        var master = await db.CollateralMasters.SingleAsync(m => m.Id == masterId);
        var latestAppraisalId = Guid.CreateVersion7();

        foreach (var (date, id, number) in new[]
                 {
                     (new DateTime(2025, 6, 1), Guid.CreateVersion7(), $"AP-M-{Guid.NewGuid():N}"[..16]),
                     (new DateTime(2026, 6, 1), latestAppraisalId,     $"AP-L-{Guid.NewGuid():N}"[..16])
                 })
        {
            master.AppendEngagement(
                appraisalId: id, appraisalNumber: number,
                requestId: Guid.CreateVersion7(), requestNumber: "RQ-HS",
                appraisalType: "ReAppraisal", appraisalDate: date,
                appraiserUserId: "tester", appraisalCompanyId: null, appraisalCompanyName: null,
                constructionInspectionFeeAmount: null, snapshot: "{}", createdAt: DateTime.Now,
                appraisedCollateralType: CollateralTypes.Land);
        }

        await db.SaveChangesAsync();

        await IngestAsync(scope, Record(
            appraisalNumber, "77006", HostLinkRecordIndicators.Drawdown, new DateOnly(2026, 1, 1)));

        var query = scope.ServiceProvider.GetRequiredService<ICollateralResultQuery>();
        var rows = await query.GetUnsentRowsAsync();

        var row = Assert.Single(rows, r => r.CollateralId == "77006");
        Assert.Equal(latestAppraisalId, row.AppraisalId);
    }

    /// <summary>A redeemed collateral keeps its id, so it is still reported — redemption is not deletion.</summary>
    [Fact]
    public async Task RedeemedMaster_StillAppearsInTheOutboundFile()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollateralDbContext>();

        var (_, appraisalNumber, appraisalId) = await SeedGroupAsync(db);

        await IngestAsync(scope, Record(
            appraisalNumber, "77007", HostLinkRecordIndicators.Redeemed, new DateOnly(2026, 4, 4)));

        var query = scope.ServiceProvider.GetRequiredService<ICollateralResultQuery>();
        var rows = await query.GetUnsentRowsAsync();

        Assert.Single(rows, r => r.AppraisalId == appraisalId && r.CollateralId == "77007");
    }
}
