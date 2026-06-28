namespace Shared.Configurations;

/// <summary>
/// Tunables for the non-Hangfire <c>BackgroundService</c> polling loops (poll/work intervals,
/// batch sizes, retry counts, lease durations). Hangfire job schedules are tuned separately via
/// the per-module <c>JobSchedules</c> table, so they are NOT covered here.
///
/// Defaults match the previously hardcoded literals, so behavior is unchanged unless overridden
/// in the "BackgroundJobs" configuration section.
/// </summary>
public class BackgroundJobsOptions
{
    public const string SectionName = "BackgroundJobs";

    /// <summary>Integration-event outbox dispatcher (<c>IntegrationEventDeliveryService</c>, one per DbContext).</summary>
    public OutboxDeliveryJobOptions OutboxDelivery { get; set; } = new();

    /// <summary>SLA monitor scanner (<c>SlaMonitorService</c>).</summary>
    public LeasedJobOptions SlaMonitor { get; set; } = new();

    /// <summary>Quotation auto-close scanner (<c>QuotationAutoCloseService</c>).</summary>
    public LeasedJobOptions QuotationAutoClose { get; set; } = new();

    /// <summary>Pool task lock-expiry sweeper (<c>TaskLockExpiryService</c>).</summary>
    public TaskLockExpiryJobOptions TaskLockExpiry { get; set; } = new();

    public void Validate()
    {
        OutboxDelivery.Validate();
        SlaMonitor.Validate(nameof(SlaMonitor));
        QuotationAutoClose.Validate(nameof(QuotationAutoClose));
        TaskLockExpiry.Validate();
    }
}

/// <summary>Tunables for the transactional outbox dispatcher.</summary>
public class OutboxDeliveryJobOptions
{
    /// <summary>Idle delay between polls when no messages are pending (and on lease-miss / error).</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Maximum number of outbox messages claimed per batch.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>Maximum delivery attempts before a message is marked Failed.</summary>
    public int MaxRetries { get; set; } = 5;

    /// <summary>How long the DB-backed processing lease is held before another instance can claim it.</summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);

    public void Validate()
    {
        if (PollInterval <= TimeSpan.Zero)
            throw new InvalidOperationException("BackgroundJobs:OutboxDelivery:PollInterval must be positive");
        if (BatchSize < 1)
            throw new InvalidOperationException("BackgroundJobs:OutboxDelivery:BatchSize must be at least 1");
        if (MaxRetries < 0)
            throw new InvalidOperationException("BackgroundJobs:OutboxDelivery:MaxRetries cannot be negative");
        if (LeaseDuration <= TimeSpan.Zero)
            throw new InvalidOperationException("BackgroundJobs:OutboxDelivery:LeaseDuration must be positive");
    }
}

/// <summary>
/// Cadence tunables shared by services built on <c>LeasedBackgroundService</c>.
/// Keep <c>LeaseDuration</c> comfortably larger than <c>WorkInterval</c> plus worst-case tick
/// runtime so the lease is not stolen mid-work between ticks.
/// </summary>
public class LeasedJobOptions
{
    /// <summary>How long the lease is held before another instance can claim an expired one.</summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(180);

    /// <summary>Interval between successive ticks when this instance is the leader.</summary>
    public TimeSpan WorkInterval { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Interval between lease-acquire attempts when this instance is not the leader.</summary>
    public TimeSpan StandbyPollInterval { get; set; } = TimeSpan.FromSeconds(5);

    public void Validate(string section)
    {
        if (WorkInterval <= TimeSpan.Zero)
            throw new InvalidOperationException($"BackgroundJobs:{section}:WorkInterval must be positive");
        if (StandbyPollInterval <= TimeSpan.Zero)
            throw new InvalidOperationException($"BackgroundJobs:{section}:StandbyPollInterval must be positive");
        if (LeaseDuration <= TimeSpan.Zero)
            throw new InvalidOperationException($"BackgroundJobs:{section}:LeaseDuration must be positive");
        if (LeaseDuration < WorkInterval)
            throw new InvalidOperationException(
                $"BackgroundJobs:{section}:LeaseDuration must be greater than or equal to WorkInterval");
    }
}

/// <summary>Tunables for the pool task lock-expiry sweeper.</summary>
public class TaskLockExpiryJobOptions
{
    /// <summary>Interval between sweeps for expired pool-task locks.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>How long a pool-task lock may be held before it is force-released.</summary>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromMinutes(30);

    public void Validate()
    {
        if (Interval <= TimeSpan.Zero)
            throw new InvalidOperationException("BackgroundJobs:TaskLockExpiry:Interval must be positive");
        if (LockTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException("BackgroundJobs:TaskLockExpiry:LockTimeout must be positive");
    }
}
