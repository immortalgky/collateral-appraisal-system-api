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

    /// <summary>What to configure when no certificate is found — names both keys the lookup honours.</summary>
    internal const string CertificateHint =
        $"Set '{ThumbprintKey}' (or '{FallbackThumbprintKey}') to the thumbprint of the secrets certificate, " +
        "and make sure the app identity can read its private key.";

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

        var certificate = LoadSecretsCertificate(configuration)
                          ?? throw new InvalidOperationException(
                              $"{encrypted.Length} encrypted configuration value(s) found but no certificate " +
                              "thumbprint is configured. " + CertificateHint);
        return DecryptAndApply(configuration, encrypted, certificate, certificate.Thumbprint);
    }

    /// <summary>
    /// The configured secrets-certificate thumbprint: <see cref="ThumbprintKey"/>, else
    /// <see cref="FallbackThumbprintKey"/>; null when neither is set. A blank primary value counts as
    /// unset — the production template renders the key even when its variable is left empty, and the
    /// deployment docs promise that empty falls back to the DataProtection cert. This is the single
    /// thumbprint lookup for configuration decryption and <see cref="ColumnSecretCipher"/>.
    /// </summary>
    internal static string? ResolveSecretsThumbprint(IConfiguration configuration)
    {
        var thumbprint = configuration[ThumbprintKey];
        if (string.IsNullOrWhiteSpace(thumbprint))
            thumbprint = configuration[FallbackThumbprintKey];
        return string.IsNullOrWhiteSpace(thumbprint) ? null : thumbprint;
    }

    /// <summary>
    /// The secrets certificate (with private key) for configuration decryption; null when no thumbprint
    /// is set. <see cref="ColumnSecretCipher"/> uses the same two steps per call (so it can follow a rotation).
    /// </summary>
    internal static X509Certificate2? LoadSecretsCertificate(IConfiguration configuration)
    {
        var thumbprint = ResolveSecretsThumbprint(configuration);
        return thumbprint is null ? null : LoadSecretsCertificateByThumbprint(thumbprint);
    }

    /// <summary>How the secrets certificate is loaded — shared with <see cref="ColumnSecretCipher"/>.</summary>
    internal static X509Certificate2 LoadSecretsCertificateByThumbprint(string thumbprint) =>
        CertificateProvider.LoadFromStoreByThumbprint(thumbprint, requirePrivateKey: true);

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
