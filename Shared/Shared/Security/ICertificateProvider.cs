using System.Security.Cryptography.X509Certificates;

namespace Shared.Security;

/// <summary>
/// Provides certificates for OAuth2 token signing and encryption
/// </summary>
public interface ICertificateProvider
{
    /// <summary>
    /// Gets the certificate used for token signing
    /// </summary>
    X509Certificate2 GetSigningCertificate();

    /// <summary>
    /// Gets the certificate used for token encryption
    /// </summary>
    X509Certificate2 GetEncryptionCertificate();
}