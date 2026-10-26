using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Collateral.Contracts.FileInterface;
using Dapper;
using Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data;

namespace Integration.Collateral.Integration.Tests;

/// <summary>
/// End-to-end cover for the outbound InternalValuerCode.
///
/// The value crosses a module boundary: the appraisal's assignment stores the appraiser's USERNAME,
/// and the code lives on <c>auth.AspNetUsers.EmployeeId</c>. <c>collateral.vw_CollateralResultExport</c>
/// joins the two. These tests exercise that join as well as the 4-character fitting rule — the unit
/// tests only cover the latter.
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

    /// <summary>
    /// Seeds a completed reappraisal in the file's scope (Purpose '03', completed after the cutoff — see
    /// <see cref="CollateralResultSeed"/>), assigned internally to <paramref name="appraiserUserName"/>.
    /// </summary>
    private static async Task<Guid> SeedCompletedReviewAsync(
        AppraisalDbContext db, ISqlConnectionFactory factory, string appraiserUserName)
    {
        var requestId = await CollateralResultSeed.InsertRequestAsync(factory);
        var appraisal = CollateralResultSeed.NewCompleted(requestId, CollateralResultSeed.InScope);

        db.Appraisals.Add(appraisal);
        await db.SaveChangesAsync();

        db.AppraisalAssignments.Add(AppraisalAssignment.Create(
            appraisal.Id, "Internal", assigneeUserId: appraiserUserName, assignedBy: "test"));
        await db.SaveChangesAsync();

        return appraisal.Id;
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
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var factory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();

        var userName = await SeedUserAsync(factory, "06327");
        var appraisalId = await SeedCompletedReviewAsync(db, factory, userName);

        var row = await GetRowAsync(scope, appraisalId);

        Assert.Equal("6327", row.InternalValuerCode);
    }

    [Fact]
    public async Task EmployeeIdThatCannotFit_IsSentBlankRatherThanTruncated()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var factory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();

        // Five significant digits: truncating would name employee 8101, a different person.
        var userName = await SeedUserAsync(factory, "81018");
        var appraisalId = await SeedCompletedReviewAsync(db, factory, userName);

        var row = await GetRowAsync(scope, appraisalId);

        Assert.Null(row.InternalValuerCode);
    }

    [Fact]
    public async Task AppraiserWithNoEmployeeId_LeavesTheCodeBlank()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

        var factory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();

        var appraisalId = await SeedCompletedReviewAsync(db, factory, "no.such.user");

        var row = await GetRowAsync(scope, appraisalId);

        Assert.Null(row.InternalValuerCode);
    }

    [Fact]
    public async Task ExternalAppraisal_SendsTheCompanyPairAndBlanksTheInternalOne()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var factory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();

        // The assignee also resolves to an employee id: the internal pair must still go out blank.
        var userName = await SeedUserAsync(factory, "06327");
        var companyId = Guid.NewGuid();
        await factory.GetOpenConnection().ExecuteAsync(
            """
            INSERT INTO auth.Companies (Id, Name, HostCompanyCode, IsActive, IsDeleted, LoanTypes)
            VALUES (@companyId, N'K-TAC Appraisal', N'KTAC', 1, 0, N'[]')
            """,
            new { companyId });

        var requestId = await CollateralResultSeed.InsertRequestAsync(factory);
        var appraisal = CollateralResultSeed.NewCompleted(requestId, CollateralResultSeed.InScope);
        db.Appraisals.Add(appraisal);
        await db.SaveChangesAsync();
        db.AppraisalAssignments.Add(AppraisalAssignment.Create(
            appraisal.Id, "External", assigneeUserId: userName, assigneeCompanyId: companyId.ToString(), assignedBy: "test"));
        await db.SaveChangesAsync();

        var row = await GetRowAsync(scope, appraisal.Id);

        Assert.Null(row.InternalValuerCode);
        Assert.Null(row.InternalValuerName);
        Assert.Equal("KTAC", row.ExternalValuerCode);
        Assert.Equal("K-TAC Appraisal", row.ExternalValuerName);
    }
}
