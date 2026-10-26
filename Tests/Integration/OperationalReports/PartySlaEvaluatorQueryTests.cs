using System.Data;
using Dapper;
using Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.OperationalReports.Shared;
using Shared.Data;

namespace Integration.OperationalReports;

/// <summary>
/// PartySlaEvaluator is static Dapper over workflow and appraisal tables, so run it against the real schema: a
/// column that does not exist (the appointment ORDER BY once named CreatedOn) fails here, not in the report.
/// It also pins which appointment clips a vendor cycle: that cycle's own visit, the latest non-cancelled
/// appointment of this appraisal dated inside the cycle, never another round's, another appraisal's, a
/// cancelled or rejected assignment's, or the most recently created row.
///
/// Rows are inserted raw, not through the aggregate, so no domain event, outbox row or workflow is started;
/// every id is unique per run because the shared database is never cleaned between tests.
/// </summary>
[Collection("Integration")]
public class PartySlaEvaluatorQueryTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task Vendor_cycle_starts_at_the_latest_non_cancelled_appointment_inside_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var requestId = Guid.CreateVersion7();

        using var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var connection = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>().GetOpenConnection();

        // The later-created row carries the EARLIER date; the latest date is cancelled.
        var appraisalId = await SeedAppraisalAsync(connection, requestId);
        var assignmentId = await SeedAssignmentAsync(connection, appraisalId, "Assigned");
        await SeedAppointmentAsync(connection, assignmentId, "2026-09-08 09:00", "Appointed", "2026-08-30 10:00");
        await SeedAppointmentAsync(connection, assignmentId, "2026-09-03 09:00", "Appointed", "2026-08-31 10:00");
        await SeedAppointmentAsync(connection, assignmentId, "2026-09-09 09:00", "Cancelled", "2026-09-01 10:00");

        await SeedLegAsync(connection, requestId, "ext-appraisal-execution", "2026-09-01 09:00", "2026-09-10 17:00");

        var result = await Evaluate(scope, requestId, appraisalId, ct);

        var cycle = Assert.Single(result.Vendor.Cycles);
        Assert.Equal(new DateTime(2026, 9, 8, 9, 0, 0), cycle.CycleStart);
    }

    [Fact]
    public async Task Visits_of_cancelled_or_rejected_assignments_and_of_other_appraisals_do_not_clip()
    {
        var ct = TestContext.Current.CancellationToken;
        var requestId = Guid.CreateVersion7();

        using var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var connection = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>().GetOpenConnection();

        var appraisalId = await SeedAppraisalAsync(connection, requestId);
        var liveAssignment = await SeedAssignmentAsync(connection, appraisalId, "InProgress");
        await SeedAppointmentAsync(connection, liveAssignment, "2026-09-04 09:00", "Appointed", "2026-09-01 10:00");

        // Cancelling or rejecting an assignment leaves its appointments 'Appointed', all dated later.
        var cancelledAssignment = await SeedAssignmentAsync(connection, appraisalId, "Cancelled");
        await SeedAppointmentAsync(connection, cancelledAssignment, "2026-09-07 15:00", "Appointed", "2026-09-01 11:00");
        var rejectedAssignment = await SeedAssignmentAsync(connection, appraisalId, "Rejected");
        await SeedAppointmentAsync(connection, rejectedAssignment, "2026-09-07 16:00", "Appointed", "2026-09-01 12:00");

        // Another appraisal of the same request, also dated later.
        var otherAppraisalId = await SeedAppraisalAsync(connection, requestId);
        var otherAssignment = await SeedAssignmentAsync(connection, otherAppraisalId, "Assigned");
        await SeedAppointmentAsync(connection, otherAssignment, "2026-09-07 12:00", "Appointed", "2026-09-01 13:00");

        await SeedLegAsync(connection, requestId, "ext-appraisal-execution", "2026-09-01 09:00", "2026-09-10 17:00");

        var result = await Evaluate(scope, requestId, appraisalId, ct);

        var cycle = Assert.Single(result.Vendor.Cycles);
        Assert.Equal(new DateTime(2026, 9, 4, 9, 0, 0), cycle.CycleStart);
    }

    [Fact]
    public async Task Each_rework_cycle_is_clipped_by_its_own_visit_and_never_starts_after_it_ends()
    {
        var ct = TestContext.Current.CancellationToken;
        var requestId = Guid.CreateVersion7();

        using var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var connection = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>().GetOpenConnection();

        var appraisalId = await SeedAppraisalAsync(connection, requestId);
        var assignmentId = await SeedAssignmentAsync(connection, appraisalId, "Assigned");
        await SeedAppointmentAsync(connection, assignmentId, "2026-09-03 09:00", "Appointed", "2026-09-01 10:00"); // round 1
        await SeedAppointmentAsync(connection, assignmentId, "2026-09-12 09:00", "Appointed", "2026-09-10 10:00"); // round 2

        await SeedVendorBankVendorLegsAsync(connection, requestId);

        var result = await Evaluate(scope, requestId, appraisalId, ct);

        Assert.Equal(2, result.Vendor.Cycles.Count);
        Assert.Equal(new DateTime(2026, 9, 3, 9, 0, 0), result.Vendor.Cycles[0].CycleStart);
        Assert.Equal(new DateTime(2026, 9, 12, 9, 0, 0), result.Vendor.Cycles[1].CycleStart);
        Assert.All(result.Vendor.Cycles, c => Assert.True(c.CycleStart <= c.CycleEnd));
    }

    [Fact]
    public async Task A_rework_cycle_with_no_visit_inside_it_stays_unclipped()
    {
        var ct = TestContext.Current.CancellationToken;
        var requestId = Guid.CreateVersion7();

        using var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var connection = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>().GetOpenConnection();

        var appraisalId = await SeedAppraisalAsync(connection, requestId);
        var assignmentId = await SeedAssignmentAsync(connection, appraisalId, "Assigned");
        await SeedAppointmentAsync(connection, assignmentId, "2026-09-03 09:00", "Appointed", "2026-09-01 10:00"); // round 1 only

        await SeedVendorBankVendorLegsAsync(connection, requestId);

        var result = await Evaluate(scope, requestId, appraisalId, ct);

        Assert.Equal(2, result.Vendor.Cycles.Count);
        Assert.Equal(new DateTime(2026, 9, 3, 9, 0, 0), result.Vendor.Cycles[0].CycleStart);
        Assert.Equal(new DateTime(2026, 9, 10, 9, 0, 0), result.Vendor.Cycles[1].CycleStart); // the leg's AssignedAt
    }

    // vendor 09-01..09-05 -> bank rework 09-05..09-10 -> vendor again 09-10..09-15
    private static async Task SeedVendorBankVendorLegsAsync(IDbConnection connection, Guid requestId)
    {
        await SeedLegAsync(connection, requestId, "ext-appraisal-execution", "2026-09-01 09:00", "2026-09-05 17:00");
        await SeedLegAsync(connection, requestId, "int-appraisal-check", "2026-09-05 17:00", "2026-09-10 09:00");
        await SeedLegAsync(connection, requestId, "ext-appraisal-execution", "2026-09-10 09:00", "2026-09-15 17:00");
    }

    private static async Task<PartySlaResult> Evaluate(
        IServiceScope scope, Guid requestId, Guid appraisalId, CancellationToken ct)
    {
        var evaluator = scope.ServiceProvider.GetRequiredService<IPartySlaEvaluator>();
        var result = await evaluator.EvaluateAsync(requestId, appraisalId, null, null, null, null, ct);
        Assert.NotNull(result);
        return result;
    }

    private static async Task<Guid> SeedAppraisalAsync(IDbConnection connection, Guid requestId)
    {
        var appraisalId = Guid.CreateVersion7();
        await connection.ExecuteAsync(
            """
            INSERT INTO appraisal.Appraisals
                (Id, RequestId, AppraisalNumber, AppraisalType, Priority, Status, IsPma, IsDeleted, CreatedAt, CreatedBy)
            VALUES (@AppraisalId, @RequestId, @AppraisalNumber, 'New', 'Normal', 'Pending', 0, 0, SYSDATETIME(), 'U1')
            """,
            new { AppraisalId = appraisalId, RequestId = requestId, AppraisalNumber = $"SLA-{Guid.NewGuid():N}"[..16] });
        return appraisalId;
    }

    private static async Task<Guid> SeedAssignmentAsync(IDbConnection connection, Guid appraisalId, string status)
    {
        var assignmentId = Guid.CreateVersion7();
        await connection.ExecuteAsync(
            """
            INSERT INTO appraisal.AppraisalAssignments
                (Id, AppraisalId, AssignmentType, AssignmentStatus, AssignmentMethod, AssignedBy)
            VALUES (@AssignmentId, @AppraisalId, 'External', @Status, 'Manual', 'U1')
            """,
            new { AssignmentId = assignmentId, AppraisalId = appraisalId, Status = status });
        return assignmentId;
    }

    private static Task SeedAppointmentAsync(
        IDbConnection connection, Guid assignmentId, string date, string status, string createdAt) =>
        connection.ExecuteAsync(
            """
            INSERT INTO appraisal.Appointments
                (Id, AssignmentId, AppointmentDateTime, AppointedBy, Status, RequiresApproval, RescheduleCount, CreatedAt)
            VALUES (@Id, @AssignmentId, @Date, 'U1', @Status, 0, 0, @CreatedAt)
            """,
            new
            {
                Id = Guid.CreateVersion7(),
                AssignmentId = assignmentId,
                Date = DateTime.Parse(date),
                Status = status,
                CreatedAt = DateTime.Parse(createdAt)
            });

    private static Task SeedLegAsync(
        IDbConnection connection, Guid requestId, string activityId, string assignedAt, string completedAt) =>
        connection.ExecuteAsync(
            """
            INSERT INTO workflow.CompletedTasks
                (Id, CorrelationId, TaskName, TaskStatus, AssignedTo, AssignedType, AssignedAt,
                 ActionTaken, CompletedAt, Movement, ActivityId, AssigneeAssignedAt)
            VALUES
                (@Id, @RequestId, 'Task', 'Completed', 'U1', 'User', @AssignedAt,
                 'Approved', @CompletedAt, 'Forward', @ActivityId, @AssignedAt)
            """,
            new
            {
                Id = Guid.CreateVersion7(),
                RequestId = requestId,
                ActivityId = activityId,
                AssignedAt = DateTime.Parse(assignedAt),
                CompletedAt = DateTime.Parse(completedAt)
            });
}
