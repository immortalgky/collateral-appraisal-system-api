using Microsoft.Extensions.DependencyInjection;

namespace Auth.Infrastructure.HealthChecks;

public static class AuthHealthCheckExtensions
{
    /// <summary>
    /// Registers the Auth module's external-dependency health checks (LDAP / Active Directory).
    /// Tagged <c>external</c> — included in <c>/health</c> and <c>/health/external</c>, excluded
    /// from <c>/health/ready</c> so a directory outage never removes a node from load balancing.
    /// </summary>
    public static IHealthChecksBuilder AddAuthHealthChecks(this IHealthChecksBuilder builder) =>
        builder.AddCheck<LdapHealthCheck>("ldap", tags: ["external", "ldap"]);
}
