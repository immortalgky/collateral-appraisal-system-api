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
/// Block projects in the AS400 HOST_COLLATERAL_LINK feed.
///
/// AS400 mints one collateral id per unit it financed and stamps the PROJECT's appraisal number on
/// every one of them, so a single project appraisal arrives as several rows with different ids. The
/// project has one <c>CollateralMaster</c> with one id slot, so writing any of those rows would let
/// one unit dictate the whole project's pledge state — one redeemed unit would mark the project
/// redeemed and drop every unit of it from the regulatory export.
///
/// These tests pin that the ingestor writes none of them, and that ordinary collateral is untouched.
/// </summary>
[Collection("Integration")]
public class HostCollateralLinkIngestorProjectTests(IntegrationTestFixture fixture)
{
    private IServiceScope CreateScope()
        => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    private static ParsedHostLinkRecord Record(
        string appraisalNumber, string hostId, string indicator = HostLinkRecordIndicators.Drawdown,
        DateOnly? date = null)
        => new(appraisalNumber, hostId, date ?? new DateOnly(2026, 1, 25), indicator,
            RowHash: $"{appraisalNumber}:{hostId}");

    /// <summary>
    /// Seeds an IsMaster collateral master carrying a single engagement for a completed appraisal.
    /// <paramref name="appraisedCollateralType"/> is what the ingestor branches on.
    /// </summary>
    private static async Task<Guid> SeedMasterWithEngagementAsync(
        CollateralDbContext db, string appraisalNumber, string appraisedCollateralType)
    {
        var master = appraisedCollateralType == CollateralTypes.Project
            ? CollateralMasterEntity.CreateProject("U", "Test Condo Project")
            : CollateralMasterEntity.CreateLand(
                ownerName: "Test Owner",
                landOfficeCode: "0100",
                province: "10",
                district: "1001",
                subDistrict: "100101",
                titleType: "NS4",
                titleNumber: $"T-{Guid.NewGuid():N}"[..12],
                surveyNumber: null,
                landParcelNumber: null,
                rawang: null,
                street: null,
                village: null,
                latitude: null,
                longitude: null);

        var appraisalId = Guid.CreateVersion7();
        master.AppendEngagement(
            appraisalId: appraisalId,
            appraisalNumber: appraisalNumber,
            requestId: Guid.CreateVersion7(),
            requestNumber: "RQ-HOSTLINK",
            appraisalType: "New",
            appraisalDate: DateTime.Now,
            appraiserUserId: "tester",
            appraisalCompanyId: null,
            appraisalCompanyName: null,
            constructionInspectionFeeAmount: null,
            snapshot: "{}",
            createdAt: DateTime.Now,
            appraisedCollateralType: appraisedCollateralType);

        db.CollateralMasters.Add(master);
        await db.SaveChangesAsync();
        return appraisalId;
    }

    /// <summary>Reads back the master the ingestor should (or should not) have written.</summary>
    private static async Task<CollateralMasterEntity> LoadMasterAsync(CollateralDbContext db, Guid appraisalId)
        => await db.CollateralMasters.AsNoTracking()
            .SingleAsync(m => db.CollateralEngagements
                .Any(e => e.AppraisalId == appraisalId && e.CollateralMasterId == m.Id));

    [Fact]
    public async Task BlockProjectRows_AreReportedAndLeftUnwritten()
    {
        var appraisalNumber = $"AP-PRJ-{Guid.NewGuid():N}"[..18];

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollateralDbContext>();
        var ingestor = scope.ServiceProvider.GetRequiredService<IHostCollateralLinkIngestor>();

        var appraisalId = await SeedMasterWithEngagementAsync(db, appraisalNumber, CollateralTypes.Project);

        // Three financed units of one project, all carrying the project's appraisal number.
        var parsed = new ParsedHostLinkFile(
            new DateOnly(2026, 6, 1),
            [
                Record(appraisalNumber, "25909"),
                Record(appraisalNumber, "25910"),
                Record(appraisalNumber, "25911")
            ]);

        var result = await ingestor.IngestAsync("AS400_COLLATLINK_20260601.txt", new DateOnly(2026, 6, 1), parsed);

        Assert.Equal(1, result.ProjectSkipped);
        Assert.Equal(0, result.Updated);
        Assert.Equal(0, result.NotFound);

        var master = await LoadMasterAsync(db, appraisalId);
        Assert.Null(master.HostCollateralId);
        Assert.False(master.IsRedeemed);
    }

    [Fact]
    public async Task OneRedeemedUnit_DoesNotMarkTheWholeProjectRedeemed()
    {
        var appraisalNumber = $"AP-PRJ-{Guid.NewGuid():N}"[..18];

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollateralDbContext>();
        var ingestor = scope.ServiceProvider.GetRequiredService<IHostCollateralLinkIngestor>();

        var appraisalId = await SeedMasterWithEngagementAsync(db, appraisalNumber, CollateralTypes.Project);

        // Two units still pledged, one redeemed last month. Under the old collapse rule the redemption
        // had the newest date and therefore won, stamping 'R' on the project.
        var parsed = new ParsedHostLinkFile(
            new DateOnly(2026, 6, 1),
            [
                Record(appraisalNumber, "25909", HostLinkRecordIndicators.Drawdown, new DateOnly(2025, 1, 25)),
                Record(appraisalNumber, "25910", HostLinkRecordIndicators.Drawdown, new DateOnly(2025, 1, 25)),
                Record(appraisalNumber, "25911", HostLinkRecordIndicators.Redeemed, new DateOnly(2026, 5, 31))
            ]);

        var result = await ingestor.IngestAsync("AS400_COLLATLINK_20260601.txt", new DateOnly(2026, 6, 1), parsed);

        Assert.Equal(1, result.ProjectSkipped);

        var master = await LoadMasterAsync(db, appraisalId);
        Assert.False(master.IsRedeemed);
        Assert.Null(master.RedeemedDate);
    }

    [Fact]
    public async Task OrdinaryCollateral_IsStillWritten()
    {
        var appraisalNumber = $"AP-LAND-{Guid.NewGuid():N}"[..18];

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollateralDbContext>();
        var ingestor = scope.ServiceProvider.GetRequiredService<IHostCollateralLinkIngestor>();

        var appraisalId = await SeedMasterWithEngagementAsync(db, appraisalNumber, CollateralTypes.Land);

        var parsed = new ParsedHostLinkFile(
            new DateOnly(2026, 6, 1),
            [Record(appraisalNumber, "25909")]);

        var result = await ingestor.IngestAsync("AS400_COLLATLINK_20260601.txt", new DateOnly(2026, 6, 1), parsed);

        Assert.Equal(0, result.ProjectSkipped);
        Assert.Equal(1, result.Updated);

        var master = await LoadMasterAsync(db, appraisalId);
        Assert.Equal("25909", master.HostCollateralId);
        Assert.False(master.IsRedeemed);
    }
}
