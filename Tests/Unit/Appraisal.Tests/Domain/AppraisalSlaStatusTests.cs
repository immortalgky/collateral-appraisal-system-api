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
    }

    [Fact]
    public void Refresh_job_is_registered_hourly_under_its_admin_id()
    {
        var job = Assert.Single(AppraisalRecurringJobs.All, j => j.JobId == "appraisal-sla-status-refresh");

        Assert.Equal("0 * * * *", job.DefaultCron);
    }
}
