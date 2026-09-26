using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Shared.Configuration;

namespace Shared.Security;

/// <summary>
/// Encrypts secrets stored in database columns with the same certificate and <c>ENC:v1:</c> format
/// as configuration secrets (<see cref="SecretProtector"/>), so the key never lives in the database
/// and a value can be produced offline with <c>tools/CasSecretTool</c>.
/// <para>
/// Reads tolerate legacy plaintext (anything without the <c>ENC:</c> prefix is returned as-is), so
/// rows written before encryption keep working until re-entered. Writes without a certificate are
/// allowed only in Development; elsewhere they throw rather than silently store plaintext.
/// </para>
/// <para>
/// Every failure is a <see cref="SecretCipherException"/>, which the API answers with a fixed message:
/// the detail (thumbprint, store location, config keys) goes to the server log only.
/// </para>
/// </summary>
public sealed class ColumnSecretCipher
{
    // The certificate is resolved per call from live configuration and cached by thumbprint:
    //  - a missing or unreadable cert fails only the call that needs it (a delivery turns that into a
    //    normal failure), never the DI resolution of every dependant — legacy plaintext rows keep working;
    //  - adding the thumbprint, fixing the private-key ACL, or rotating to a new thumbprint takes effect
    //    on the next call without an app-pool recycle. Secrets encrypted to a retired cert must be
    //    re-entered after rotation (only the old private key can open them).
    // The cache is ONE reference to an immutable record, so a reader sees either the old pair or the new
    // one, never a torn mix (a thumbprint with a null or stale cert). Two threads racing a (re)load both
    // load the same cert; a replaced cert is not disposed, because another thread may still be using it —
    // that costs one handle per rotation, which is rare.
    private sealed record CachedCertificate(string Thumbprint, X509Certificate2 Certificate);

    private readonly Func<string?> _resolveThumbprint;
    private readonly Func<string, X509Certificate2> _loadCertificate;
    private volatile CachedCertificate? _cached;
    private readonly bool _allowPlaintextWrites;

    public ColumnSecretCipher(IConfiguration configuration, IHostEnvironment environment)
        : this(
            () => EncryptedConfigurationExtensions.ResolveSecretsThumbprint(configuration),
            EncryptedConfigurationExtensions.LoadSecretsCertificateByThumbprint,
            environment.IsDevelopment())
    {
    }

    /// <summary>Test seam: supply the certificate directly instead of loading it from the store.</summary>
    internal ColumnSecretCipher(X509Certificate2? certificate, bool allowPlaintextWrites)
        : this(() => certificate?.Thumbprint, _ => certificate!, allowPlaintextWrites)
    {
    }

    internal ColumnSecretCipher(
        Func<string?> resolveThumbprint,
        Func<string, X509Certificate2> loadCertificate,
        bool allowPlaintextWrites)
    {
        _resolveThumbprint = resolveThumbprint;
        _loadCertificate = loadCertificate;
        _allowPlaintextWrites = allowPlaintextWrites;
    }

    public string Protect(string plaintext)
    {
        var certificate = Certificate();
        if (certificate is not null)
            return Run(() => SecretProtector.Protect(plaintext, certificate), "encrypt");

        if (_allowPlaintextWrites)
            return plaintext;

        throw new SecretCipherException(
            "Cannot store a secret: no certificate is configured. " + EncryptedConfigurationExtensions.CertificateHint);
    }

    public string Unprotect(string stored)
    {
        if (!SecretProtector.IsProtected(stored))
            return stored;

        var certificate = Certificate()
                          ?? throw new SecretCipherException(
                              "Cannot read an encrypted secret: no certificate is configured. " +
                              EncryptedConfigurationExtensions.CertificateHint);

        return Run(() => SecretProtector.Unprotect(stored, certificate), "decrypt");
    }

    private X509Certificate2? Certificate()
    {
        var thumbprint = _resolveThumbprint();
        if (thumbprint is null)
            return null;

        var cached = _cached;
        if (cached is not null && string.Equals(cached.Thumbprint, thumbprint, StringComparison.OrdinalIgnoreCase))
            return cached.Certificate;

        var certificate = Run(() => _loadCertificate(thumbprint), "load the certificate for");
        _cached = new CachedCertificate(thumbprint, certificate);
        return certificate;
    }

    private static T Run<T>(Func<T> action, string what)
    {
        try
        {
            return action();
        }
        catch (Exception ex) when (ex is not SecretCipherException)
        {
            throw new SecretCipherException(
                $"Could not {what} a stored secret. " + EncryptedConfigurationExtensions.CertificateHint, ex);
        }
    }
}

/// <summary>
/// A stored secret could not be encrypted or decrypted. The message is for the server log; the API
/// answers with a fixed line so certificate and configuration details never reach the browser.
/// Derives from <see cref="InvalidOperationException"/> for callers that already catch that.
/// </summary>
public sealed class SecretCipherException : InvalidOperationException
{
    public SecretCipherException(string message) : base(message)
    {
    }

    public SecretCipherException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
