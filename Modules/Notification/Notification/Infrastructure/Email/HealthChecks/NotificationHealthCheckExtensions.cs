using Microsoft.Extensions.DependencyInjection;

namespace Notification.Infrastructure.Email.HealthChecks;

public static class NotificationHealthCheckExtensions
{
    /// <summary>
    /// Registers the Notification module's external-dependency health checks (SMTP gateway).
    /// Tagged <c>external</c> — included in <c>/health</c> and <c>/health/external</c>, excluded
    /// from <c>/health/ready</c> so a mail-gateway outage never removes a node from load balancing.
    /// </summary>
    public static IHealthChecksBuilder AddNotificationHealthChecks(this IHealthChecksBuilder builder) =>
        builder.AddCheck<SmtpHealthCheck>("smtp", tags: ["external", "smtp"]);
}
