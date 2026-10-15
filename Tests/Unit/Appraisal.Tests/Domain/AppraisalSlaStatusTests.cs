using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Scheduling;

namespace Appraisal.Tests.Domain;

/// <summary>
/// The SLA status rule (<see cref="AppraisalSla"/>) and the hourly job that applies it set-based. The rule used
/// to be <c>(due - now).Days</c>, which truncates toward zero: 12 hours overdue is 0 days, so it came out AtRisk.
/// </summary>
public class AppraisalSlaStatusTests
{
    private static readonly DateTime Now = new(2026, 10, 11, 9, 0, 0);

    [Theory]
    [InlineData(0, "Breached")]          // exactly due
    [InlineData(-1, "Breached")]         // 1 minute overdue
    [InlineData(-12 * 60, "Breached")]   // 12 hours overdue (was AtRisk)
    [InlineData(-3 * 24 * 60, "Breached")]
    [InlineData(1, "AtRisk")]            // 1 minute left
    [InlineData(47 * 60, "AtRisk")]
    [InlineData(48 * 60, "AtRisk")]      // exactly at the window edge
    [InlineData(48 * 60 + 1, "OnTrack")]
    [InlineData(49 * 60, "OnTrack")]
    public void Status_is_decided_on_the_instant_not_on_whole_days(int minutesUntilDue, string expected)
    {
        Assert.Equal(expected, AppraisalSla.StatusAt(Now.AddMinutes(minutesUntilDue), Now));
    }

    [Theory]
    [InlineData(-12 * 60, "Breached")]
    [InlineData(47 * 60, "AtRisk")]
    [InlineData(49 * 60, "OnTrack")]
    public void UpdateSlaStatus_uses_the_same_rule(int minutesUntilDue, string expected)
    {
        var appraisal = Appraisal.Domain.Appraisals.Appraisal.Create(
            requestId: Guid.NewGuid(), appraisalType: "New", priority: "Normal", now: Now,
            slaHours: 24, slaDueDate: Now.AddMinutes(minutesUntilDue));

        appraisal.UpdateSlaStatus(Now);

        Assert.Equal(expected, appraisal.SLAStatus);
    }

    [Fact]
    public void UpdateSlaStatus_without_a_due_date_leaves_the_status_alone()
    {
        var appraisal = Appraisal.Domain.Appraisals.Appraisal.Create(
            requestId: Guid.NewGuid(), appraisalType: "New", priority: "Normal", now: Now);

        appraisal.UpdateSlaStatus(Now);

        Assert.Null(appraisal.SLAStatus);
    }

    [Theory]
    [InlineData(-1, "Breached")]        // 1 minute after the due date
    [InlineData(0, "OnTrack")]          // exactly on the due date is on time, like IsWithinSLA
    [InlineData(30, "OnTrack")]
    [InlineData(47 * 60, "OnTrack")]    // closed with 47 h to spare: never AtRisk once closed
    public void ClosedStatusAt_is_Breached_only_after_the_due_date(int minutesBeforeDue, string expected)
    {
        var due = Now.AddHours(10);

        Assert.Equal(expected, AppraisalSla.ClosedStatusAt(due, due.AddMinutes(-minutesBeforeDue)));
    }

    private static Appraisal.Domain.Appraisals.Appraisal OpenWithDue(DateTime due, DateTime createdAt) =>
        Appraisal.Domain.Appraisals.Appraisal.Create(
            requestId: Guid.NewGuid(), appraisalType: "New", priority: "Normal", now: createdAt,
            slaHours: 24, slaDueDate: due);

    [Fact]
    public void Approval_after_the_due_date_settles_Breached_even_if_it_was_AtRisk()
    {
        var due = Now.AddHours(1);
        var appraisal = OpenWithDue(due, Now.AddDays(-1));
        appraisal.UpdateSlaStatus(Now);
        Assert.Equal("AtRisk", appraisal.SLAStatus); // what the 09:00 run would have written

        appraisal.MarkApprovedByCommittee("C1", due.AddMinutes(30));

        Assert.Equal("Breached", appraisal.SLAStatus);
    }

    [Fact]
    public void Approval_before_the_due_date_settles_OnTrack_not_AtRisk()
    {
        var due = Now.AddHours(30);
        var appraisal = OpenWithDue(due, Now.AddDays(-1));
        appraisal.UpdateSlaStatus(Now);
        Assert.Equal("AtRisk", appraisal.SLAStatus);

        appraisal.MarkApprovedByCommittee("C1", Now);

        Assert.Equal("OnTrack", appraisal.SLAStatus);
    }

    [Theory]
    [InlineData(30, "Breached")]
    [InlineData(-30, "OnTrack")]
    public void Complete_settles_the_status_at_the_completion_time(int minutesAfterDue, string expected)
    {
        var due = Now.AddHours(1);
        var appraisal = OpenWithDue(due, Now.AddDays(-1));
        appraisal.EnsureUnderReview();

        appraisal.Complete(due.AddMinutes(minutesAfterDue));

        Assert.Equal(expected, appraisal.SLAStatus);
    }

    [Theory]
    [InlineData(30, "Breached")]
    [InlineData(-30, "OnTrack")]
    public void Cancel_settles_the_status_at_the_cancel_time(int minutesAfterDue, string expected)
    {
        var due = Now.AddHours(1);
        var appraisal = OpenWithDue(due, Now.AddDays(-1));
        appraisal.UpdateSlaStatus(Now);

        appraisal.Cancel("u", due.AddMinutes(minutesAfterDue), "reason");

        Assert.Equal(expected, appraisal.SLAStatus);
    }

    [Fact]
    public void Closing_an_appraisal_without_a_due_date_leaves_the_status_alone()
    {
        var appraisal = Appraisal.Domain.Appraisals.Appraisal.Create(
            requestId: Guid.NewGuid(), appraisalType: "New", priority: "Normal", now: Now);

        appraisal.Cancel("u", Now, null);

        Assert.Null(appraisal.SLAStatus);
    }

    [Theory]
    [InlineData(1, "AtRisk")]          // created inside the 2-day window
    [InlineData(-5, "Breached")]       // created already past due (backdated request)
    [InlineData(72, "OnTrack")]
    public void A_new_appraisal_starts_at_the_status_its_due_date_implies(int hoursUntilDue, string expected)
    {
        var appraisal = OpenWithDue(Now.AddHours(hoursUntilDue), Now);

        Assert.Equal(expected, appraisal.SLAStatus);
    }

    [Fact]
    public void Refresh_sql_writes_only_open_appraisals_whose_status_changes_and_binds_now()
    {
        var sql = AppraisalSlaStatusRefreshJob.UpdateSql;

        Assert.Contains("a.IsDeleted = 0", sql);
        Assert.Contains("a.SLADueDate IS NOT NULL", sql);
        Assert.Contains("a.Status NOT IN ('Completed', 'Cancelled')", sql);
        Assert.Contains("ISNULL(a.SLAStatus, '') <> x.NewStatus", sql);
        Assert.Contains("a.SLADueDate <= {0} THEN 'Breached'", sql);
        Assert.Contains($"a.SLADueDate <= DATEADD(day, {AppraisalSla.AtRiskWindowDays}, {{0}}) THEN 'AtRisk'", sql);
        Assert.DoesNotContain("GETDATE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SYSDATETIME", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UpdatedAt", sql); // a derived recalculation, not an edit
        // Batched under the 5,000-lock escalation threshold.
        Assert.StartsWith($"UPDATE TOP ({AppraisalSlaStatusRefreshJob.BatchSize}) a", sql);
        Assert.InRange(AppraisalSlaStatusRefreshJob.BatchSize, 1, 4999);
    }

    [Fact]
    public void Refresh_job_is_registered_hourly_under_its_admin_id()
    {
        var job = Assert.Single(AppraisalRecurringJobs.All, j => j.JobId == "appraisal-sla-status-refresh");

        Assert.Equal("0 * * * *", job.DefaultCron);
    }
}
