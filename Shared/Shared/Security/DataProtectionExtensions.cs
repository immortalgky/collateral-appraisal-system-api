using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Shared.Security;

public static class DataProtectionExtensions
{
    private const string ApplicationName = "CollateralAppraisalSystem";

    public static IServiceCollection AddSharedDataProtection<TContext>(
        this IServiceCollection services,
        IConfiguration? configuration = null)
        where TContext : DbContext, IDataProtectionKeyContext
    {
        var builder = services.AddDataProtection()
            .SetApplicationName(ApplicationName)
            .PersistKeysToDbContext<TContext>()
            .SetDefaultKeyLifetime(TimeSpan.FromDays(90));

        // Key-at-rest encryption. Keys persisted to the DB are unencrypted unless we wrap them with
        // a certificate. When a thumbprint is configured we load that cert (requiring its private
        // key, since each node must DECRYPT the shared key ring) and encrypt the ring with it. In a
        // multi-server (N=2) deployment the SAME cert (same thumbprint, in LocalMachine\My) must be
        // installed on every app server. No thumbprint configured => keys stay unencrypted.
        var thumbprint = configuration?["DataProtection:CertificateThumbprint"];
        if (!string.IsNullOrWhiteSpace(thumbprint))
        {
            builder.ProtectKeysWithCertificate(
                CertificateProvider.LoadFromStoreByThumbprint(thumbprint, requirePrivateKey: true));
        }
        else if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Development")
        {
            // Outside Development the unencrypted default is a security gap, and a typo'd/missing
            // thumbprint lands here silently. Surface it at startup so "secure" and "silently
            // insecure" are distinguishable in the logs.
            services.AddHostedService<UnencryptedDataProtectionKeyWarning>();
        }

        return services;
    }

    private sealed class UnencryptedDataProtectionKeyWarning(
        ILogger<UnencryptedDataProtectionKeyWarning> logger) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogWarning(
                "DataProtection: no 'DataProtection:CertificateThumbprint' configured — the key ring is " +
                "persisted UNENCRYPTED. Anyone able to read the keys table can decrypt antiforgery cookies " +
                "and OpenIddict reference tokens. Configure a shared certificate thumbprint in production.");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
