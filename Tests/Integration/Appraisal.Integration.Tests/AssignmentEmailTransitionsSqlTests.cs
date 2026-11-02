using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// The Sent / Failed reports of an assignment email are conditional UPDATEs, not read-modify-write, so the two
/// consumers (separate endpoints) cannot overwrite each other with a stale read; and an abandoned attempt's Pending
/// choice is retired. Run on real SQL Server because the guard is the WHERE clause.
/// </summary>
[Collection("Integration")]
public class AssignmentEmailTransitionsSqlTests(IntegrationTestFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IServiceScope CreateScope() => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    private async Task<AssignmentEmail> SeedAsync(Guid assignmentId, bool queued)
    {
        var email = AssignmentEmail.ToSend(Guid.NewGuid(), assignmentId, [], [], "subject", "<p>body</p>", []);
        if (queued) email.Queue("ACME", "Khun A", ["company@x.test"], []);
        using var scope = CreateScope();
        scope.ServiceProvider.GetRequiredService<AppraisalDbContext>().AssignmentEmails.Add(email);
        await scope.ServiceProvider.GetRequiredService<AppraisalDbContext>().SaveChangesAsync(Ct);
        return email;
    }

    private async Task<(string Status, string? Reason, DateTime? SentAt)> ReadAsync(Guid id)
    {
        using var scope = CreateScope();
        var e = await scope.ServiceProvider.GetRequiredService<AppraisalDbContext>().AssignmentEmails.AsNoTracking()
            .SingleAsync(x => x.Id == id, Ct);
        return (e.Status, e.FailureReason, e.SentAt);
    }

    [Fact]
    public async Task A_stale_failure_report_never_overwrites_a_sent_email_but_a_redrive_recovers_a_failed_one()
    {
        var email = await SeedAsync(Guid.NewGuid(), queued: true);
        var sentAt = new DateTime(2026, 11, 2, 10, 0, 0);

        using (var scope = CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IAssignmentEmailRepository>();
            Assert.True(await repo.MarkSentIfQueuedOrFailedAsync(email.Id, sentAt, Ct));
            // The delayed failure report arrives after the confirmation.
            Assert.False(await repo.MarkFailedIfQueuedAsync(email.Id, "late failure", sentAt.AddSeconds(5), Ct));
            // A redelivered confirmation changes nothing.
            Assert.False(await repo.MarkSentIfQueuedOrFailedAsync(email.Id, sentAt.AddMinutes(1), Ct));
        }

        Assert.Equal((AssignmentEmailStatus.Sent, (string?)null, (DateTime?)sentAt), await ReadAsync(email.Id));

        var failing = await SeedAsync(Guid.NewGuid(), queued: true);
        using (var scope = CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IAssignmentEmailRepository>();
            Assert.True(await repo.MarkFailedIfQueuedAsync(failing.Id, "  refused  ", sentAt, Ct));
            Assert.Equal((AssignmentEmailStatus.Failed, (string?)"refused", (DateTime?)null), await ReadAsync(failing.Id));

            // Ops redrove the request and the gateway took it.
            Assert.True(await repo.MarkSentIfQueuedOrFailedAsync(failing.Id, sentAt, Ct));
        }

        Assert.Equal((AssignmentEmailStatus.Sent, (string?)null, (DateTime?)sentAt), await ReadAsync(failing.Id));
    }

    [Fact]
    public async Task An_abandoned_attempts_pending_choice_is_deleted_but_other_rows_and_newer_choices_stay()
    {
        var assignmentId = Guid.NewGuid();
        var stale = await SeedAsync(assignmentId, queued: false);
        var handedOver = await SeedAsync(assignmentId, queued: true);
        var otherAssignment = await SeedAsync(Guid.NewGuid(), queued: false);

        using (var scope = CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IAssignmentEmailRepository>();
            await repo.DeletePendingCreatedUpToAsync(assignmentId, DateTime.Now.AddMinutes(1), Ct);
            // A Pending row created after the cutoff belongs to the next attempt.
            var next = await SeedAsync(assignmentId, queued: false);
            await repo.DeletePendingCreatedUpToAsync(assignmentId, DateTime.Now.AddHours(-1), Ct);

            using var check = CreateScope();
            var db = check.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var ids = await db.AssignmentEmails.AsNoTracking()
                .Where(e => e.AssignmentId == assignmentId || e.Id == otherAssignment.Id).Select(e => e.Id).ToListAsync(Ct);
            Assert.DoesNotContain(stale.Id, ids);
            Assert.Contains(handedOver.Id, ids);
            Assert.Contains(otherAssignment.Id, ids);
            Assert.Contains(next.Id, ids);
        }
    }
}
