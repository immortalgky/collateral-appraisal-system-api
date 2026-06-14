namespace Shared.Scheduling;

/// <summary>
/// Schedule metadata for a Hangfire recurring job, managed by administrators. Owned per-module: each
/// module maps this entity into its own schema via <c>modelBuilder.AddJobSchedules()</c> and registers
/// its jobs via <c>app.UseModuleRecurringJobs&lt;TContext&gt;()</c>. The job id → job class mapping lives
/// in code (the module's recurring-job catalog); this row only overrides the schedule applied at startup.
/// </summary>
public class JobSchedule
{
    public const int MaxJobIdLength = 100;
    public const int MaxCronLength = 100;
    public const int MaxTimeZoneIdLength = 100;
    public const int MaxDescriptionLength = 500;

    public Guid Id { get; private set; }

    /// <summary>Hangfire recurring job id, e.g. "reappraisal-as400". Matches the module catalog key.</summary>
    public string JobId { get; private set; } = null!;

    /// <summary>Standard 5-field cron expression, e.g. "0 1 1 * *".</summary>
    public string CronExpression { get; private set; } = null!;

    /// <summary>TimeZoneInfo id; NULL means use the application default timezone.</summary>
    public string? TimeZoneId { get; private set; }

    /// <summary>When false, the job is removed from Hangfire at startup.</summary>
    public bool IsEnabled { get; private set; } = true;

    public string? Description { get; private set; }

    // Required by EF Core
    private JobSchedule()
    {
    }

    public static JobSchedule Create(
        string jobId,
        string cronExpression,
        string? timeZoneId = null,
        bool isEnabled = true,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new ArgumentException("Job id cannot be empty.", nameof(jobId));

        if (jobId.Length > MaxJobIdLength)
            throw new ArgumentException(
                $"Job id exceeds the maximum allowed length of {MaxJobIdLength} characters.",
                nameof(jobId));

        if (string.IsNullOrWhiteSpace(cronExpression))
            throw new ArgumentException("Cron expression cannot be empty.", nameof(cronExpression));

        return new JobSchedule
        {
            Id = Guid.CreateVersion7(),
            JobId = jobId.Trim(),
            CronExpression = cronExpression.Trim(),
            TimeZoneId = string.IsNullOrWhiteSpace(timeZoneId) ? null : timeZoneId.Trim(),
            IsEnabled = isEnabled,
            Description = description
        };
    }

    public void UpdateSchedule(string cronExpression, string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            throw new ArgumentException("Cron expression cannot be empty.", nameof(cronExpression));

        CronExpression = cronExpression.Trim();
        TimeZoneId = string.IsNullOrWhiteSpace(timeZoneId) ? null : timeZoneId.Trim();
    }

    public void SetEnabled(bool isEnabled)
    {
        IsEnabled = isEnabled;
    }
}
