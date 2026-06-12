using Auth.Application.Configurations;
using Auth.Application.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Auth.Infrastructure.HealthChecks;

/// <summary>
/// Probes connectivity to Active Directory by binding as the service/integrated identity.
/// When <c>Ldap:Enabled</c> is false the check reports Healthy with <c>state=disabled</c> rather
/// than failing — the integration is intentionally off (e.g. dev). Tagged <c>external</c> so it
/// surfaces in <c>/health</c> and <c>/health/external</c> but never affects the readiness probe.
/// </summary>
internal sealed class LdapHealthCheck(
    IOptions<LdapConfiguration> options,
    ILdapAuthenticationService ldap) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        if (!config.Enabled)
            return HealthCheckResult.Healthy(
                "LDAP disabled (Ldap:Enabled=false).",
                new Dictionary<string, object> { ["state"] = "disabled" });

        try
        {
            await ldap.CheckConnectionAsync(cancellationToken);
            return HealthCheckResult.Healthy($"Bound to LDAP {config.Server}:{config.Port}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                $"LDAP bind to {config.Server}:{config.Port} failed: {ex.Message}", ex);
        }
    }
}
