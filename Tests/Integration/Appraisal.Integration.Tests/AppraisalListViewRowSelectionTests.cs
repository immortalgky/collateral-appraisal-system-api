using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Dapper;
using Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// Pins the two "pick one row out of many" decisions inside <c>appraisal.vw_AppraisalList</c>:
/// the latest active assignment, and the first land property's location.
///
/// Both used to be <c>ROW_NUMBER() OVER (PARTITION BY …)</c> derived tables filtered by
/// <c>rn = 1</c> from the outside; they are now correlated <c>OUTER APPLY … TOP 1</c>. That
/// rewrite was verified against production-shaped data with EXCEPT in both directions, but that
/// check is close to vacuous for this invariant: almost every appraisal has exactly one candidate
/// row, so "same result" mostly means "there was nothing to choose". These tests deliberately
/// create the multi-candidate cases so the choice itself is asserted.
/// </summary>
[Collection("Integration")]
public class AppraisalListViewRowSelectionTests(IntegrationTestFixture fixture)
{
    private IServiceScope CreateScope()
        => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    private sealed record ListRow(
        string? AssigneeUserId,
        string? AssignmentType,
        string? AssignmentStatus,
        DateTime? AssignedDate,
        string? Province,
        string? District,
        string? SubDistrict);

    private async Task<ListRow?> ReadViewRowAsync(Guid appraisalId)
    {
        using var scope = CreateScope();
        var connectionFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        var connection = connectionFactory.GetOpenConnection();

        return await connection.QueryFirstOrDefaultAsync<ListRow>(
            """
            SELECT AssigneeUserId, AssignmentType, AssignmentStatus, AssignedDate,
                   Province, District, SubDistrict
            FROM appraisal.vw_AppraisalList
            WHERE Id = @Id
            """,
            new { Id = appraisalId });
    }

    private static AppraisalAggregate NewAppraisal(string prefix)
    {
        var appraisal = AppraisalAggregate.Create(
            requestId: Guid.CreateVersion7(),
            appraisalType: "New",
            priority: "Normal",
            now: DateTime.Now);

        appraisal.SetAppraisalNumber($"{prefix}{Guid.NewGuid():N}"[..16]);
        return appraisal;
    }

    private static void SetAssignedAt(AppraisalAssignment assignment, DateTime when)
        => typeof(AppraisalAssignment).GetProperty("AssignedAt")!.SetValue(assignment, when);

    // ── latest active assignment ─────────────────────────────────────────────

    [Fact]
    public async Task Picks_the_most_recently_assigned_assignment_when_several_are_active()
    {
        Guid appraisalId;

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

            var appraisal = NewAppraisal("VWSEL-A-");
            db.Appraisals.Add(appraisal);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = appraisal.Id;

            var older = AppraisalAssignment.Create(
                appraisalId, "External", assigneeCompanyId: Guid.NewGuid().ToString(), assignedBy: "test");
            SetAssignedAt(older, DateTime.Now.AddDays(-10));

            var newest = AppraisalAssignment.Create(
                appraisalId, "Internal", assigneeUserId: "P-NEWEST", assignedBy: "test");
            SetAssignedAt(newest, DateTime.Now.AddDays(-1));

            // Added oldest-last on purpose: insertion order must not decide the winner.
            db.AppraisalAssignments.AddRange(newest, older);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var row = await ReadViewRowAsync(appraisalId);

        Assert.NotNull(row);
        Assert.Equal("Internal", row!.AssignmentType);
        Assert.Equal("P-NEWEST", row.AssigneeUserId);
    }

    [Fact]
    public async Task Ignores_rejected_and_cancelled_assignments_even_when_they_are_the_newest()
    {
        Guid appraisalId;

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

            var appraisal = NewAppraisal("VWSEL-B-");
            db.Appraisals.Add(appraisal);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = appraisal.Id;

            var active = AppraisalAssignment.Create(
                appraisalId, "Internal", assigneeUserId: "P-ACTIVE", assignedBy: "test");
            SetAssignedAt(active, DateTime.Now.AddDays(-5));

            var cancelled = AppraisalAssignment.Create(
                appraisalId, "Internal", assigneeUserId: "P-CANCELLED", assignedBy: "test");
            SetAssignedAt(cancelled, DateTime.Now);
            cancelled.Cancel("test");

            db.AppraisalAssignments.AddRange(active, cancelled);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var row = await ReadViewRowAsync(appraisalId);

        Assert.NotNull(row);
        Assert.Equal("P-ACTIVE", row!.AssigneeUserId);
    }

    [Fact]
    public async Task Reports_no_assignment_at_all_when_every_assignment_is_terminal()
    {
        Guid appraisalId;

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

            var appraisal = NewAppraisal("VWSEL-C-");
            db.Appraisals.Add(appraisal);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = appraisal.Id;

            var cancelled = AppraisalAssignment.Create(
                appraisalId, "Internal", assigneeUserId: "P-GONE", assignedBy: "test");
            cancelled.Cancel("test");

            db.AppraisalAssignments.Add(cancelled);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var row = await ReadViewRowAsync(appraisalId);

        // The row must still appear in the list — the assignment columns are simply null.
        Assert.NotNull(row);
        Assert.Null(row!.AssigneeUserId);
        Assert.Null(row.AssignmentType);
        Assert.Null(row.AssignedDate);
    }

    // ── first land location ──────────────────────────────────────────────────

    [Fact]
    public async Task Reports_the_location_of_the_lowest_sequence_land_property()
    {
        Guid appraisalId;

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

            var appraisal = NewAppraisal("VWSEL-D-");

            var first = appraisal.AddLandProperty();
            first.LandDetail!.Update(
                coordinates: null,
                address: Address.Create("SD-FIRST", "D-FIRST", "P-FIRST"),
                landOffice: null);

            var second = appraisal.AddLandProperty();
            second.LandDetail!.Update(
                coordinates: null,
                address: Address.Create("SD-SECOND", "D-SECOND", "P-SECOND"),
                landOffice: null);

            db.Appraisals.Add(appraisal);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = appraisal.Id;
        }

        var row = await ReadViewRowAsync(appraisalId);

        Assert.NotNull(row);
        Assert.Equal("P-FIRST", row!.Province);
        Assert.Equal("D-FIRST", row.District);
        Assert.Equal("SD-FIRST", row.SubDistrict);
    }

    [Fact]
    public async Task Skips_land_properties_that_have_no_province()
    {
        Guid appraisalId;

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

            var appraisal = NewAppraisal("VWSEL-E-");

            // Sequence 1 has no address at all, so the view must fall through to sequence 2.
            appraisal.AddLandProperty();

            var withAddress = appraisal.AddLandProperty();
            withAddress.LandDetail!.Update(
                coordinates: null,
                address: Address.Create("SD-REAL", "D-REAL", "P-REAL"),
                landOffice: null);

            db.Appraisals.Add(appraisal);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = appraisal.Id;
        }

        var row = await ReadViewRowAsync(appraisalId);

        Assert.NotNull(row);
        Assert.Equal("P-REAL", row!.Province);
    }

    [Fact]
    public async Task Sequence_numbers_are_unique_per_appraisal_so_the_first_property_is_unambiguous()
    {
        // The view orders the location APPLY by (SequenceNumber, Id). The Id half is defensive
        // only: this unique index means a tie cannot occur in the first place. Asserting the index
        // here keeps that reasoning honest — if it is ever relaxed, the tiebreaker stops being
        // decoration and this test says so.
        using var scope = CreateScope();
        var connectionFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        var connection = connectionFactory.GetOpenConnection();

        var isUnique = await connection.QueryFirstOrDefaultAsync<bool?>(
            """
            SELECT is_unique
            FROM sys.indexes
            WHERE object_id = OBJECT_ID('appraisal.AppraisalProperties')
              AND name = 'IX_AppraisalProperties_AppraisalId_SequenceNumber'
            """);

        Assert.True(isUnique, "IX_AppraisalProperties_AppraisalId_SequenceNumber must stay unique.");
    }
}
