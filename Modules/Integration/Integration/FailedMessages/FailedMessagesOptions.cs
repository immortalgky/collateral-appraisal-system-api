namespace Integration.FailedMessages;

/// <summary>
/// Tunables for the per-node failed-message collector (docs/failed-messages/design.md §3, D10).
/// Broker credentials/host/management URL are NOT configured here — they reuse the app's existing
/// "RabbitMQ" section (design assumption 3 / D8), so the collector never has its own set of secrets to manage.
/// </summary>
public class FailedMessagesOptions
{
    public const string SectionName = "FailedMessages";

    /// <summary>Master switch — <see cref="FailedMessageCollectorService"/> is only registered when true.</summary>
    public bool Enabled { get; set; }

    /// <summary>How often each collector round (discover, collect, retry, snapshot) runs.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Max messages drained per <c>_error</c>/<c>_skipped</c> queue, per round.</summary>
    public int BatchPerQueue { get; set; } = 100;

    /// <summary>Days a <c>Kind = Skipped</c> row may stay <c>Pending</c> (counted from the later of <c>CollectedAt</c> and the last admin <c>ActionAt</c>) before
    /// <see cref="FailedMessageCleanupJob"/> purges it. A Skipped message has no consumer, so retrying it cannot help
    /// and a broken binding would otherwise grow the table without bound. Must be between 1 and <see cref="MaxSkippedPendingRetentionDays"/>. Applies whether or
    /// not <see cref="Enabled"/> — the cleanup job runs regardless of the collector.</summary>
    public int SkippedPendingRetentionDays { get; set; } = 30;

    /// <summary>Upper bound for <see cref="SkippedPendingRetentionDays"/> (ten years): <c>AddDays(-days)</c> throws once the
    /// cutoff leaves <see cref="DateTime"/>'s range, which would abort the whole nightly cleanup job.</summary>
    public const int MaxSkippedPendingRetentionDays = 3650;

    public const string DefaultManagementUrl = "http://localhost:15672";

    /// <summary>RabbitMQ management API base URL for this node's own broker (design: localhost per node).
    /// Populated from <c>RabbitMQ:ManagementUrl</c> by <c>IntegrationModule</c>, not from the FailedMessages section.
    /// The app's AMQP username/password are re-sent as HTTP Basic auth on every round, so <see cref="Validate"/>
    /// rejects plain <c>http://</c> to a non-loopback host (https anywhere, or http to loopback, is fine).</summary>
    public string ManagementUrl { get; set; } = DefaultManagementUrl;

    /// <summary>Set by <c>IntegrationModule</c> when the pre-move <c>FailedMessages:ManagementUrl</c> key is still in
    /// configuration. It is never read (no fallback), so <see cref="Validate"/> fails loudly instead of ignoring it.</summary>
    public bool LegacyManagementUrlPresent { get; set; }

    /// <summary>Same ValidateOnStart/PostConfigure pattern as
    /// <c>Shared.Configurations.BackgroundJobsOptions</c> — fail fast at host startup on a bad cadence
    /// instead of the collector silently never running (Interval &lt;= 0) or draining nothing every
    /// round (BatchPerQueue &lt;= 0). Collector settings are skipped while <see cref="Enabled"/> is false (a disabled
    /// collector never reads them, so a bad one must not block API startup); <see cref="SkippedPendingRetentionDays"/>
    /// is always checked, because <see cref="FailedMessageCleanupJob"/> runs whether or not the collector does.</summary>
    public void Validate()
    {
        if (SkippedPendingRetentionDays is < 1 or > MaxSkippedPendingRetentionDays)
            throw new InvalidOperationException(
                $"FailedMessages:SkippedPendingRetentionDays must be between 1 and {MaxSkippedPendingRetentionDays}");

        if (!Enabled)
            return;

        if (Interval <= TimeSpan.Zero)
            throw new InvalidOperationException("FailedMessages:Interval must be positive");
        if (BatchPerQueue < 1)
            throw new InvalidOperationException("FailedMessages:BatchPerQueue must be at least 1");

        if (LegacyManagementUrlPresent)
            throw new InvalidOperationException(
                "FailedMessages:ManagementUrl has moved to RabbitMQ:ManagementUrl — move the value and remove the old key");
        if (!Uri.TryCreate(ManagementUrl, UriKind.Absolute, out var url)
            || url.Scheme is not ("http" or "https"))
            throw new InvalidOperationException("RabbitMQ:ManagementUrl must be an absolute http(s) URL");
        if (url.Scheme == "http" && !url.IsLoopback)
            throw new InvalidOperationException(
                $"RabbitMQ:ManagementUrl uses plain http to non-loopback host '{url.Host}', which would send "
                + "the RabbitMQ password in cleartext (HTTP Basic auth) every round — use https, or http to localhost");
    }
}
