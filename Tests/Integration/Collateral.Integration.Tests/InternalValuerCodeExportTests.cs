using Collateral.Contracts;
using Collateral.Contracts.FileInterface;
using Collateral.Data;
using Dapper;
using Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data;
using CollateralMasterEntity = Collateral.CollateralMasters.Models.CollateralMaster;

namespace Integration.Collateral.Integration.Tests;

/// <summary>
/// End-to-end cover for the outbound InternalValuerCode.
///
/// The value crosses a module boundary: the engagement stores the appraiser's USERNAME, and the code
/// lives on <c>auth.AspNetUsers.EmployeeId</c>, which the Collateral DbContext cannot see. These tests
/// exercise the Dapper join as well as the 4-character fitting rule — the unit tests only cover the
/// latter.
/// </summary>
[Collection("Integration")]
public class InternalValuerCodeExportTests(IntegrationTestFixture fixture)
{
    private IServiceScope CreateScope()
        => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    /// <summary>Inserts a bank-staff user carrying the given employee id and returns its username.</summary>
    private static async Task<string> SeedUserAsync(ISqlConnectionFactory factory, string employeeId)
    {
        var userName = $"valuer.{Guid.NewGuid():N}"[..24];
        var connection = factory.GetOpenConnection();

        await connection.ExecuteAsync(
            """
            INSERT INTO auth.AspNetUsers
                (Id, UserName, FirstName, LastName, EmployeeId, EmailConfirmed, PhoneNumberConfirmed,
                 TwoFactorEnabled, LockoutEnabled, AccessFailedCount, AuthSource, IsActive, MustChangePassword)
            VALUES
                (NEWID(), @UserName, N'Test', N'Valuer', @EmployeeId, 0, 0, 0, 0, 0, N'Local', 1, 0)
            """,
            new { UserName = userName, EmployeeId = employeeId });

        return userName;
    }

    /// <summary>Seeds a land master whose single engagement already carries an AS400 collateral id.</summary>
    private static async Task<Guid> SeedSentReadyEngagementAsync(
        CollateralDbContext db, string appraiserUserName)
    {
        var master = CollateralMasterEntity.CreateLand(
            ownerName: "Test Owner",
            landOfficeCode: "0100",
            province: "10",
            district: "1001",
            subDistrict: "100101",
            titleType: "DEED",
            titleNumber: $"IVC-{Guid.NewGuid():N}"[..20],
            surveyNumber: null, landParcelNumber: null, rawang: null,
            street: null, village: null, latitude: null, longitude: null);

        var appraisalId = Guid.CreateVersion7();
        master.AppendEngagement(
            appraisalId: appraisalId,
            appraisalNumber: $"IVC{Guid.NewGuid():N}"[..10],
            requestId: Guid.CreateVersion7(),
            requestNumber: "REQ-IVC",
            appraisalType: "New",
            appraisalDate: DateTime.Now,
            appraiserUserId: appraiserUserName,
            appraisalCompanyId: null,
            appraisalCompanyName: null,
            constructionInspectionFeeAmount: null,
            snapshot: "{}",
            createdAt: DateTime.Now,
            appraisedCollateralType: CollateralTypes.Land);

        // The AS400 id sits on the master, not the engagement — that is the grain AS400 keys.
        master.ApplyHostDrawdown("25909");

        db.CollateralMasters.Add(master);
        await db.SaveChangesAsync();
        return appraisalId;
    }

    private static async Task<CollateralResultRow> GetRowAsync(IServiceScope scope, Guid appraisalId)
    {
        var query = scope.ServiceProvider.GetRequiredService<ICollateralResultQuery>();
        var rows = await query.GetUnsentRowsAsync();
        return Assert.Single(rows, r => r.AppraisalId == appraisalId);
    }

    [Fact]
    public async Task ZeroPaddedEmployeeId_IsSentWithoutItsLeadingZero()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollateralDbContext>();
        var factory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();

        var userName = await SeedUserAsync(factory, "06327");
        var appraisalId = await SeedSentReadyEngagementAsync(db, userName);

        var row = await GetRowAsync(scope, appraisalId);

        Assert.Equal("6327", row.InternalValuerCode);
    }

    [Fact]
    public async Task EmployeeIdThatCannotFit_IsSentBlankRatherThanTruncated()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollateralDbContext>();
        var factory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();

        // Five significant digits: truncating would name employee 8101, a different person.
        var userName = await SeedUserAsync(factory, "81018");
        var appraisalId = await SeedSentReadyEngagementAsync(db, userName);

        var row = await GetRowAsync(scope, appraisalId);

        Assert.Null(row.InternalValuerCode);
    }

    [Fact]
    public async Task AppraiserWithNoEmployeeId_LeavesTheCodeBlank()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollateralDbContext>();

        var appraisalId = await SeedSentReadyEngagementAsync(db, "no.such.user");

        var row = await GetRowAsync(scope, appraisalId);

        Assert.Null(row.InternalValuerCode);
        Assert.Equal("25909", row.CollateralId);
    }
}
