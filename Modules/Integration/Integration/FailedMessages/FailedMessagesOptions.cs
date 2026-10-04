namespace Integration.FailedMessages;

/// <summary>
/// Tunables for the per-node failed-message collector (docs/failed-messages/design.md §3, D10).
/// Broker credentials/host are NOT here — they reuse the app's existing "RabbitMQ" section
/// (design assumption 3 / D8), so the collector never has its own set of secrets to manage.
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

    /// <summary>RabbitMQ management API base URL for this node's own broker (design: localhost per node).
    /// The app's AMQP username/password are re-sent as HTTP Basic auth on every round, so <see cref="Validate"/>
    /// rejects plain <c>http://</c> to a non-loopback host (https anywhere, or http to loopback, is fine).</summary>
    public string ManagementUrl { get; set; } = "http://localhost:15672";

    /// <summary>Same ValidateOnStart/PostConfigure pattern as
    /// <c>Shared.Configurations.BackgroundJobsOptions</c> — fail fast at host startup on a bad cadence
    /// instead of the collector silently never running (Interval &lt;= 0) or draining nothing every
    /// round (BatchPerQueue &lt;= 0). Skipped entirely while <see cref="Enabled"/> is false: a disabled
    /// collector never reads these values, so a bad one must not block API startup.</summary>
    public void Validate()
    {
        if (!Enabled)
            return;

        if (Interval <= TimeSpan.Zero)
            throw new InvalidOperationException("FailedMessages:Interval must be positive");
        if (BatchPerQueue < 1)
            throw new InvalidOperationException("FailedMessages:BatchPerQueue must be at least 1");

        if (!Uri.TryCreate(ManagementUrl, UriKind.Absolute, out var url)
            || url.Scheme is not ("http" or "https"))
            throw new InvalidOperationException("FailedMessages:ManagementUrl must be an absolute http(s) URL");
        if (url.Scheme == "http" && !url.IsLoopback)
            throw new InvalidOperationException(
                $"FailedMessages:ManagementUrl uses plain http to non-loopback host '{url.Host}', which would send "
                + "the RabbitMQ password in cleartext (HTTP Basic auth) every round — use https, or http to localhost");
    }
}
