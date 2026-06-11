namespace Notification.Infrastructure.Email;

/// <summary>
/// Configuration bound from the <c>Mail</c> appsettings section.
/// Mirrors <c>ReportingConfiguration</c> in the Reporting module.
/// </summary>
public sealed class MailConfiguration
{
    public const string SectionName = "Mail";

    /// <summary>SMTP host name or IP address.</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>SMTP port number. Common values: 25 (plain), 587 (STARTTLS), 465 (SSL).</summary>
    public int Port { get; set; } = 25;

    /// <summary>When true the adapter negotiates STARTTLS. When false the connection is plain.</summary>
    public bool UseStartTls { get; set; }

    /// <summary>SMTP username. Leave empty for anonymous servers (e.g. smtp4dev in dev).</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>SMTP password. Leave empty when <see cref="Username"/> is empty.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>The From address used for all outbound mail. Gateway sender-confirmation rule: fixed by config, not by callers.</summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>Optional display name shown alongside the From address.</summary>
    public string? FromDisplayName { get; set; }

    /// <summary>
    /// Master on/off switch. When false the adapter logs and no-ops — no SMTP connection is attempted.
    /// Set to false in CI or when no mail catcher is running.
    /// </summary>
    public bool Enabled { get; set; }
}
