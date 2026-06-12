using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Notification.Contracts.Email;

namespace Notification.Infrastructure.Email.HealthChecks;

/// <summary>
/// Probes connectivity to the SMTP gateway by connecting (and authenticating if configured) and
/// issuing a NOOP — no message is sent. When <c>Mail:Enabled</c> is false the check reports Healthy
/// with <c>state=disabled</c>. Tagged <c>external</c> so it surfaces in <c>/health</c> and
/// <c>/health/external</c> but never affects the readiness probe.
/// </summary>
internal sealed class SmtpHealthCheck(
    IOptions<MailConfiguration> options,
    IEmailSender sender) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        if (!config.Enabled)
            return HealthCheckResult.Healthy(
                "SMTP disabled (Mail:Enabled=false).",
                new Dictionary<string, object> { ["state"] = "disabled" });

        try
        {
            await sender.CheckConnectionAsync(cancellationToken);
            return HealthCheckResult.Healthy($"Connected to SMTP {config.Host}:{config.Port}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                $"SMTP connect to {config.Host}:{config.Port} failed: {ex.Message}", ex);
        }
    }
}
