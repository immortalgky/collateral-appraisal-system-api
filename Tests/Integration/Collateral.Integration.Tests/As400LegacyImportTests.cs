using Collateral.Contracts;
using Collateral.Contracts.As400Legacy;
using Collateral.Contracts.FileInterface;
using Collateral.Data;
using Dapper;
using Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data;
using CollateralMasterEntity = Collateral.CollateralMasters.Models.CollateralMaster;

namespace Integration.Collateral.Integration.Tests;

/// <summary>
/// Importing the AS400 legacy collateral listing — collateral the bank held before this system
/// existed, valued in AS400 and never appraised in CAS.
///
/// The listing carries a collateral id, a valuation date and a value, but no title number and no
/// location. That shapes everything: there is nothing to dedup on, so the collateral id is the only
/// handle, and a master minted from it carries no detail row.
///
/// <c>appraisal.AS400ReportListing</c> is supplied by the bank rather than by our migrations, so
/// these tests create it themselves — the same shape the bank delivers.
/// </summary>
[Collection("Integration")]
public class As400LegacyImportTests(IntegrationTestFixture fixture)
{
    private IServiceScope CreateScope()
        => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    private static async Task EnsureListingTableAsync(ISqlConnectionFactory factory)
        => await factory.GetOpenConnection().ExecuteAsync("""
            IF OBJECT_ID('appraisal.AS400ReportListing') IS NULL
            CREATE TABLE appraisal.AS400ReportListing (
                RecordType                     nchar(1)      NOT NULL,
                ApplicationId                  nchar(10)     NULL,
                NewestApplicationId            nchar(10)     NULL,
                CollateralID                   decimal(19,0) NULL,
                UnderConstruction              nchar(1)      NULL,
                ProcessOfConstruction          decimal(5,2)  NULL,
                AppraisalValueAsCompleted      decimal(15,2) NULL,
                AppraisalValueAtTheOrigination decimal(15,2) NULL,
                ValuationDate                  date          NULL,
                ValuationPriceInBaht           decimal(15,2) NULL)
            """);

    /// <summary>Each test owns its own rows; the table is shared, so clear by application number.</summary>
    private static async Task SeedListingAsync(
        ISqlConnectionFactory factory, string applicationId, string collateralId,
        DateTime valuationDate, decimal price)
        => await factory.GetOpenConnection().ExecuteAsync("""
            DELETE FROM appraisal.AS400ReportListing WHERE RTRIM(ApplicationId) = @ApplicationId;
            INSERT INTO appraisal.AS400ReportListing
                (RecordType, ApplicationId, NewestApplicationId, CollateralID, ValuationDate, ValuationPriceInBaht)
            VALUES ('D', @ApplicationId, @ApplicationId, @CollateralId, @ValuationDate, @Price);
            """,
            new { ApplicationId = applicationId, CollateralId = decimal.Parse(collateralId), ValuationDate = valuationDate, Price = price });

    private static async Task ClearListingAsync(ISqlConnectionFactory factory)
        => await factory.GetOpenConnection().ExecuteAsync(
            "IF OBJECT_ID('appraisal.AS400ReportListing') IS NOT NULL DELETE FROM appraisal.AS400ReportListing");

    /// <summary>A land master carrying one engagement and an AS400 collateral id.</summary>
    private static async Task<Guid> SeedMasterWithIdAsync(
        CollateralDbContext db, string hostCollateralId, DateTime appraisalDate)
    {
        var master = CollateralMasterEntity.CreateLand(
            ownerName: "Legacy Owner", landOfficeCode: "0100",
            province: "10", district: "1001", subDistrict: "100101",
            titleType: "NS4", titleNumber: $"LG-{Guid.NewGuid():N}"[..14],
            surveyNumber: null, landParcelNumber: null, rawang: null,
            street: null, village: null, latitude: null, longitude: null);

        master.AppendEngagement(
            appraisalId: Guid.CreateVersion7(), appraisalNumber: $"AP-LG-{Guid.NewGuid():N}"[..16],
            requestId: Guid.CreateVersion7(), requestNumber: "RQ-LG",
            appraisalType: "New", appraisalDate: appraisalDate,
            appraiserUserId: null, appraisalCompanyId: null, appraisalCompanyName: null,
            constructionInspectionFeeAmount: null, snapshot: "{}", createdAt: DateTime.Now,
            appraisedCollateralType: CollateralTypes.Land);

        master.ApplyHostDrawdown(hostCollateralId);

        db.CollateralMasters.Add(master);
        await db.SaveChangesAsync();
        return master.Id;
    }

    private static Task<As400LegacyImportResult> ImportAsync(IServiceScope scope, params string[] reported)
        => scope.ServiceProvider.GetRequiredService<IAs400LegacyImporter>()
            .ImportAsync(reported.ToHashSet(StringComparer.Ordinal));

    // ── Rule 1: the collateral is already ours ────────────────────────────────────────────────

    /// <summary>
    /// A listing row whose collateral id already reaches a master is that master's older history.
    /// It must attach there, not mint a second master for the same physical collateral — there is no
    /// tool to merge two once they exist.
    /// </summary>
    [Fact]
    public async Task CollateralAlreadyKnown_AttachesToThatMaster_DoesNotCreateOne()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollateralDbContext>();
        var factory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        await EnsureListingTableAsync(factory);
        await ClearListingAsync(factory);

        var hostId = $"90{Random.Shared.Next(100000, 999999)}";
        var appId = $"99A{Random.Shared.Next(10000, 99999)}";
        var masterId = await SeedMasterWithIdAsync(db, hostId, new DateTime(2025, 1, 9));
        await SeedListingAsync(factory, appId, hostId, new DateTime(2006, 11, 24), 2_590_000m);

        var unkBefore = await db.CollateralMasters.CountAsync(m => m.CollateralType == CollateralTypes.Unidentified);

        var result = await ImportAsync(scope, appId);

        Assert.Equal(1, result.Attached);
        Assert.Equal(0, result.Created);

        var engagements = await db.CollateralEngagements.AsNoTracking()
            .Where(e => e.CollateralMasterId == masterId).ToListAsync();
        Assert.Equal(2, engagements.Count);

        // The AS400 valuation is the oldest, which is the whole point of importing it.
        var earliest = engagements.OrderBy(e => e.AppraisalDate).First();
        Assert.Equal(appId, earliest.AppraisalNumber);
        Assert.Equal(2_590_000m, earliest.AppraisalValue);

        Assert.Equal(unkBefore, await db.CollateralMasters
            .CountAsync(m => m.CollateralType == CollateralTypes.Unidentified));
    }

    /// <summary>
    /// Rule 1 wins over rule 2. A row can satisfy both — AS400 still reports it under its 99A number
    /// even though a CAS appraisal exists — and testing them the other way round mints a duplicate.
    /// </summary>
    [Fact]
    public async Task KnownCollateral_StillReported_StillAttaches_RatherThanCreating()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollateralDbContext>();
        var factory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        await EnsureListingTableAsync(factory);
        await ClearListingAsync(factory);

        var hostId = $"91{Random.Shared.Next(100000, 999999)}";
        var appId = $"99A{Random.Shared.Next(10000, 99999)}";
        await SeedMasterWithIdAsync(db, hostId, new DateTime(2024, 1, 5));
        await SeedListingAsync(factory, appId, hostId, new DateTime(2012, 2, 14), 1_000_000m);

        // appId IS in the reported set — rule 2 would fire if the order were wrong.
        var result = await ImportAsync(scope, appId);

        Assert.Equal(1, result.Attached);
        Assert.Equal(0, result.Created);
    }

    // ── Rule 2: never appraised here, still held ──────────────────────────────────────────────

    /// <summary>
    /// Unknown collateral that AS400 still reports gets a master of its own — with no detail row,
    /// because the listing carries no identity to put in one.
    /// </summary>
    [Fact]
    public async Task UnknownButStillReported_CreatesUnidentifiedMasterWithNoDetail()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollateralDbContext>();
        var factory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        await EnsureListingTableAsync(factory);
        await ClearListingAsync(factory);

        var appId = $"99A{Random.Shared.Next(10000, 99999)}";
        await SeedListingAsync(factory, appId, $"92{Random.Shared.Next(100000, 999999)}",
            new DateTime(2011, 3, 17), 5_890_000m);

        var result = await ImportAsync(scope, appId);

        Assert.Equal(1, result.Created);
        Assert.Equal(0, result.Attached);

        var engagement = await db.CollateralEngagements.AsNoTracking()
            .SingleAsync(e => e.AppraisalNumber == appId);
        var master = await db.CollateralMasters.AsNoTracking()
            .Include(m => m.LandDetail).Include(m => m.CondoDetail)
            .Include(m => m.LeaseholdDetail).Include(m => m.MachineDetail).Include(m => m.ProjectDetail)
            .SingleAsync(m => m.Id == engagement.CollateralMasterId);

        Assert.Equal(CollateralTypes.Unidentified, master.CollateralType);
        Assert.True(master.IsMaster);
        // No detail row of any type — that absence is what keeps it out of every dedup lookup, so a
        // real appraisal of some parcel can never be merged onto it.
        Assert.Null(master.LandDetail);
        Assert.Null(master.CondoDetail);
        Assert.Null(master.LeaseholdDetail);
        Assert.Null(master.MachineDetail);
        Assert.Null(master.ProjectDetail);
    }

    // ── Rule 3: released ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Absent from the link file means AS400 has stopped reporting it — the bank released it.
    /// Creating a master would be inventing an asset the bank does not hold.
    /// </summary>
    [Fact]
    public async Task UnknownAndNotReported_CreatesNothing()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollateralDbContext>();
        var factory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        await EnsureListingTableAsync(factory);
        await ClearListingAsync(factory);

        var appId = $"99A{Random.Shared.Next(10000, 99999)}";
        await SeedListingAsync(factory, appId, $"93{Random.Shared.Next(100000, 999999)}",
            new DateTime(2013, 6, 12), 6_100_000m);

        // Reported set deliberately names a different application.
        var result = await ImportAsync(scope, "99A00000");

        Assert.Equal(1, result.SkippedNotHeld);
        Assert.Equal(0, result.Created);
        Assert.Equal(0, result.Attached);
        Assert.False(await db.CollateralEngagements.AnyAsync(e => e.AppraisalNumber == appId));
    }

    // ── The date guard ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The master's LATEST engagement decides the outbound file and every master-level screen, so an
    /// AS400 valuation newer than everything the master holds must not be attached — it would
    /// displace the current CAS figures with older AS400 ones.
    /// </summary>
    [Fact]
    public async Task WouldBecomeTheLatestEngagement_IsSkipped()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollateralDbContext>();
        var factory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        await EnsureListingTableAsync(factory);
        await ClearListingAsync(factory);

        var hostId = $"94{Random.Shared.Next(100000, 999999)}";
        var appId = $"99A{Random.Shared.Next(10000, 99999)}";
        var masterId = await SeedMasterWithIdAsync(db, hostId, new DateTime(2015, 5, 5));
        // Newer than the master's only engagement.
        await SeedListingAsync(factory, appId, hostId, new DateTime(2020, 1, 1), 9_000_000m);

        var result = await ImportAsync(scope, appId);

        Assert.Equal(1, result.SkippedWouldBeLatest);
        Assert.Equal(0, result.Attached);
        Assert.Equal(1, await db.CollateralEngagements.CountAsync(e => e.CollateralMasterId == masterId));
    }

    // ── Idempotency ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunningTwice_ImportsNothingTheSecondTime()
    {
        using var scope = CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        await EnsureListingTableAsync(factory);
        await ClearListingAsync(factory);

        var appId = $"99A{Random.Shared.Next(10000, 99999)}";
        await SeedListingAsync(factory, appId, $"95{Random.Shared.Next(100000, 999999)}",
            new DateTime(2014, 11, 3), 3_200_000m);

        var first = await ImportAsync(scope, appId);
        var second = await ImportAsync(scope, appId);

        Assert.Equal(1, first.Created);
        Assert.Equal(0, second.Created);
        Assert.Equal(1, second.AlreadyPresent);
    }

    // ── The one that matters most ─────────────────────────────────────────────────────────────

    /// <summary>
    /// A legacy engagement must never reach the outbound COLLATERAL_RESULT file.
    ///
    /// Its value came FROM AS400; sending it back would report an appraisal we never performed. And
    /// it qualifies on every other count the query tests — the master carries an id, it is IsMaster,
    /// it is not deleted, the engagement is the master's latest, and nothing has been logged against
    /// its synthetic AppraisalId. Only the AppraisalType filter stops it.
    /// </summary>
    [Fact]
    public async Task LegacyEngagement_IsNeverSentBackToAs400()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollateralDbContext>();
        var factory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        await EnsureListingTableAsync(factory);
        await ClearListingAsync(factory);

        var hostId = $"96{Random.Shared.Next(100000, 999999)}";
        var appId = $"99A{Random.Shared.Next(10000, 99999)}";
        await SeedListingAsync(factory, appId, hostId, new DateTime(2012, 9, 5), 2_290_000m);

        await ImportAsync(scope, appId);

        // Give the master its AS400 id, exactly as the nightly link ingest would.
        var engagement = await db.CollateralEngagements.SingleAsync(e => e.AppraisalNumber == appId);
        var master = await db.CollateralMasters.SingleAsync(m => m.Id == engagement.CollateralMasterId);
        master.ApplyHostDrawdown(hostId);
        await db.SaveChangesAsync();

        var rows = await scope.ServiceProvider.GetRequiredService<ICollateralResultQuery>()
            .GetUnsentRowsAsync();

        Assert.DoesNotContain(rows, r => r.CollateralId == hostId);
        Assert.DoesNotContain(rows, r => r.AppraisalReportNumber == appId);
    }

    /// <summary>
    /// The other half of that filter, and the one that actually bit.
    ///
    /// When a legacy valuation shares its date with the master's newest CAS appraisal it sorts ahead
    /// of it (it was inserted later, so it wins the CreatedAt tiebreak). If the query picks the
    /// master's representative first and rejects legacy rows second, that master emits NOTHING — the
    /// real appraisal is not the latest, and the latest is filtered out. Collateral the bank holds
    /// then vanishes from the file with no error. It happened to 135 masters on a production-like
    /// import before the filter was moved inside the subquery.
    /// </summary>
    [Fact]
    public async Task LegacyEngagementSharingTheLatestDate_DoesNotHideTheRealAppraisal()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollateralDbContext>();
        var factory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        await EnsureListingTableAsync(factory);
        await ClearListingAsync(factory);

        var hostId = $"97{Random.Shared.Next(100000, 999999)}";
        var appId = $"99A{Random.Shared.Next(10000, 99999)}";
        var sharedDate = new DateTime(2024, 6, 1);

        var masterId = await SeedMasterWithIdAsync(db, hostId, sharedDate);
        // Same date as the real appraisal — passes the importer's "strictly newer" guard.
        await SeedListingAsync(factory, appId, hostId, sharedDate, 1_500_000m);

        var result = await ImportAsync(scope, appId);
        Assert.Equal(1, result.Attached);

        var rows = await scope.ServiceProvider.GetRequiredService<ICollateralResultQuery>()
            .GetUnsentRowsAsync();

        // The real appraisal must still be sent, carrying the master's id...
        var row = Assert.Single(rows, r => r.CollateralId == hostId);
        // ...and it must be the CAS one, never the legacy valuation.
        Assert.NotEqual(appId, row.AppraisalReportNumber);

        var realAppraisalId = await db.CollateralEngagements.AsNoTracking()
            .Where(e => e.CollateralMasterId == masterId && e.AppraisalType != "AS400Legacy")
            .Select(e => e.AppraisalId).SingleAsync();
        Assert.Equal(realAppraisalId, row.AppraisalId);
    }
}
