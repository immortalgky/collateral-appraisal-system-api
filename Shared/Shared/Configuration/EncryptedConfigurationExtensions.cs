using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Shared.Security;

namespace Shared.Configuration;

/// <summary>
/// Decrypts <c>ENC:v1:...</c> configuration values in place at startup so that the rest of the
/// application (connection strings, <c>IOptions&lt;T&gt;</c> bindings, etc.) reads plaintext and
/// needs no changes. See <see cref="SecretProtector"/> for the format and rationale.
/// </summary>
public static class EncryptedConfigurationExtensions
{
    /// <summary>Configuration key holding the thumbprint of the secrets certificate.</summary>
    private const string ThumbprintKey = "Secrets:CertificateThumbprint";

    /// <summary>
    /// Fallback thumbprint key: reuse the DataProtection certificate if a dedicated secrets
    /// certificate is not configured. Keeps single-cert deployments simple.
    /// </summary>
    private const string FallbackThumbprintKey = "DataProtection:CertificateThumbprint";

    /// <summary>
    /// Scans the already-loaded configuration for <c>ENC:v1:</c> values, decrypts them with the
    /// configured certificate (loaded from the machine store by thumbprint), and layers the
    /// plaintext back on top as the highest-precedence source. No-ops when nothing is encrypted
    /// (e.g. Development / tests), so it is safe to call unconditionally as the first line after
    /// <c>WebApplication.CreateBuilder(args)</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown at startup if encrypted values exist but no thumbprint is configured, or if any
    /// value fails to decrypt. The message names the offending key but never its value.
    /// </exception>
    public static IConfigurationManager AddDecryptedSecrets(this IConfigurationManager configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var encrypted = FindEncrypted(configuration);
        if (encrypted.Length == 0)
            return configuration;

        var thumbprint = configuration[ThumbprintKey] ?? configuration[FallbackThumbprintKey];
        if (string.IsNullOrWhiteSpace(thumbprint))
        {
            throw new InvalidOperationException(
                $"{encrypted.Length} encrypted configuration value(s) found but no certificate " +
                $"thumbprint is configured. Set '{ThumbprintKey}' (or '{FallbackThumbprintKey}') " +
                "to the thumbprint of the certificate that can decrypt them.");
        }

        var certificate = CertificateProvider.LoadFromStoreByThumbprint(thumbprint, requirePrivateKey: true);
        return DecryptAndApply(configuration, encrypted, certificate, thumbprint);
    }

    /// <summary>
    /// Test seam: decrypt using a supplied certificate instead of loading one from the store.
    /// </summary>
    internal static IConfigurationManager AddDecryptedSecrets(
        this IConfigurationManager configuration, X509Certificate2 certificate)
    {
        var encrypted = FindEncrypted(configuration);
        return encrypted.Length == 0
            ? configuration
            : DecryptAndApply(configuration, encrypted, certificate, certificate.Thumbprint);
    }

    private static KeyValuePair<string, string?>[] FindEncrypted(IConfiguration configuration) =>
        configuration.AsEnumerable()
            .Where(kvp => SecretProtector.IsProtected(kvp.Value))
            .ToArray();

    private static IConfigurationManager DecryptAndApply(
        IConfigurationManager configuration,
        KeyValuePair<string, string?>[] encrypted,
        X509Certificate2 certificate,
        string thumbprint)
    {
        var decrypted = new Dictionary<string, string?>(encrypted.Length);
        foreach (var (key, value) in encrypted)
        {
            try
            {
                decrypted[key] = SecretProtector.Unprotect(value!, certificate);
            }
            catch (Exception ex)
            {
                // Never include the value or plaintext in the message — only the key.
                throw new InvalidOperationException(
                    $"Failed to decrypt configuration value '{key}'. Check that the certificate " +
                    $"(thumbprint '{thumbprint}') is installed with its private key and that the " +
                    "value was encrypted with the same certificate.", ex);
            }
        }

        configuration.AddInMemoryCollection(decrypted);
        return configuration;
    }
}
