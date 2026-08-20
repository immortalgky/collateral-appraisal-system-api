using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Security;

namespace Shared.Configuration;

/// <summary>Registration helper for <see cref="PlaintextSecretAudit"/>.</summary>
public static class PlaintextSecretAuditExtensions
{
    /// <summary>
    /// Registers the audit as a hosted service, but only outside the Development environment where
    /// plaintext local-docker credentials are expected.
    /// </summary>
    public static IServiceCollection AddPlaintextSecretAudit(
        this IServiceCollection services, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
            services.AddHostedService<PlaintextSecretAudit>();
        return services;
    }
}

/// <summary>
/// Startup guard that logs an error for any known secret-bearing configuration key still holding
/// a plaintext value outside Development. This is what stops the config from silently drifting
/// back to plaintext when a new integration is added — it never reads or logs the value itself,
/// only the key name. Modelled on <c>UnencryptedDataProtectionKeyWarning</c>.
/// </summary>
public sealed class PlaintextSecretAudit(
    IConfiguration configuration,
    ILogger<PlaintextSecretAudit> logger) : IHostedService
{
    /// <summary>
    /// Keys whose values must be encrypted in production. Connection strings are treated
    /// specially (see <see cref="StartAsync"/>) because Integrated Security has no password.
    /// </summary>
    private static readonly string[] ConnectionStringKeys =
    [
        "ConnectionStrings:Database",
        "ConnectionStrings:Hangfire",
        "ConnectionStrings:Redis",
    ];

    private static readonly string[] SecretKeys =
    [
        "RabbitMQ:Password",
        "Mail:Password",
        "Ldap:BindPassword",
        "SeedData:AdminUser:Password",
        "FileTransfer:Inbound:Sftp:Password",
        "FileTransfer:Outbound:Sftp:Password",
    ];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var offenders = new List<string>();

        foreach (var key in SecretKeys)
        {
            var value = configuration[key];
            if (IsPlaintextSecret(value))
                offenders.Add(key);
        }

        // A connection string is only a concern when it actually carries a password; Integrated
        // Security connection strings have none and must not be flagged.
        foreach (var key in ConnectionStringKeys)
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value)
                && value.Contains("Password=", StringComparison.OrdinalIgnoreCase)
                && !SecretProtector.IsProtected(value))
            {
                offenders.Add(key);
            }
        }

        if (offenders.Count > 0)
        {
            logger.LogError(
                "SECURITY: {Count} configuration secret(s) are stored in PLAINTEXT: {Keys}. " +
                "Encrypt them with the CasSecretTool ({Prefix}...) so no credential sits in " +
                "plaintext in appsettings. (Values are never logged.)",
                offenders.Count, string.Join(", ", offenders), SecretProtector.Prefix);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>A value is a plaintext secret if it is non-empty and not already encrypted.</summary>
    private static bool IsPlaintextSecret(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !SecretProtector.IsProtected(value);
}
