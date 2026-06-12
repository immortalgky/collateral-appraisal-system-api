using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography.X509Certificates;

namespace Shared.Security;

/// <summary>
/// Production-ready certificate provider that loads certificates from various sources
/// </summary>
public class CertificateProvider : ICertificateProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<CertificateProvider> _logger;
    private readonly bool _isDevelopment;

    public CertificateProvider(IConfiguration configuration, ILogger<CertificateProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
    }

    public X509Certificate2 GetSigningCertificate()
    {
        if (_isDevelopment)
        {
            _logger.LogWarning("Using development signing certificate. This should not be used in production!");
            return GetDevelopmentCertificate("signing");
        }

        return LoadProductionCertificate("OAuth2:SigningCertificate");
    }

    public X509Certificate2 GetEncryptionCertificate()
    {
        if (_isDevelopment)
        {
            _logger.LogWarning("Using development encryption certificate. This should not be used in production!");
            return GetDevelopmentCertificate("encryption");
        }

        return LoadProductionCertificate("OAuth2:EncryptionCertificate");
    }

    private X509Certificate2 LoadProductionCertificate(string configurationKey)
    {
        var certificateConfig = _configuration.GetSection(configurationKey);
        var source = certificateConfig["Source"];

        return source?.ToLower() switch
        {
            "file" => LoadFromFile(certificateConfig),
            "store" => LoadFromStore(certificateConfig),
            "keyvault" => LoadFromKeyVault(certificateConfig),
            _ => throw new InvalidOperationException($"Unknown certificate source: {source}. Valid sources: file, store, keyvault")
        };
    }

    private X509Certificate2 LoadFromFile(IConfiguration config)
    {
        var path = config["Path"] ?? throw new ArgumentNullException("Certificate file path not specified");
        var password = config["Password"];

        if (!File.Exists(path))
            throw new FileNotFoundException($"Certificate file not found: {path}");

        _logger.LogInformation("Loading certificate from file: {Path}", path);
        return new X509Certificate2(path, password);
    }

    private X509Certificate2 LoadFromStore(IConfiguration config)
    {
        var storeName = Enum.Parse<StoreName>(config["StoreName"] ?? "My");
        var storeLocation = Enum.Parse<StoreLocation>(config["StoreLocation"] ?? "LocalMachine");
        var thumbprint = config["Thumbprint"] ?? throw new ArgumentNullException("Certificate thumbprint not specified");

        using var store = new X509Store(storeName, storeLocation);
        store.Open(OpenFlags.ReadOnly);

        var certificates = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
        
        if (certificates.Count == 0)
            throw new InvalidOperationException($"Certificate with thumbprint {thumbprint} not found in {storeLocation}/{storeName}");

        _logger.LogInformation("Loading certificate from store: {StoreLocation}/{StoreName}, Thumbprint: {Thumbprint}", 
            storeLocation, storeName, thumbprint);
        
        return certificates[0];
    }

    /// <summary>
    /// Loads a certificate by thumbprint from the machine certificate stores, trying
    /// <c>LocalMachine\My</c> then <c>CurrentUser\My</c>. Shared by production cert loading and
    /// DataProtection key-at-rest encryption so the store-lookup logic lives in one place.
    /// </summary>
    /// <param name="thumbprint">Certificate thumbprint (spaces / MMC copy artefacts are tolerated).</param>
    /// <param name="requirePrivateKey">
    /// When true, throws if the matched cert has no accessible private key — required when the cert
    /// must DECRYPT (e.g. unprotecting the DataProtection key ring on another node/restart).
    /// </param>
    public static X509Certificate2 LoadFromStoreByThumbprint(string thumbprint, bool requirePrivateKey = false)
    {
        // Strip spaces and the invisible char the Windows MMC "copy thumbprint" can prepend.
        var normalized = new string(thumbprint.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        foreach (var location in new[] { StoreLocation.LocalMachine, StoreLocation.CurrentUser })
        {
            try
            {
                using var store = new X509Store(StoreName.My, location);
                store.Open(OpenFlags.ReadOnly);
                var found = store.Certificates.Find(X509FindType.FindByThumbprint, normalized, validOnly: false);
                if (found.Count == 0)
                    continue;

                var certificate = found[0];
                if (requirePrivateKey && !certificate.HasPrivateKey)
                    throw new InvalidOperationException(
                        $"Certificate '{normalized}' was found in {location}\\My but has no accessible private key. " +
                        "A private key is required to decrypt protected data; install the cert with its private key " +
                        "and grant the app-pool identity read access.");

                return certificate;
            }
            catch (Exception ex) when (
                ex is System.Security.Cryptography.CryptographicException
                    or PlatformNotSupportedException
                    or System.Security.SecurityException
                    or UnauthorizedAccessException)
            {
                // Store unavailable on this platform/location (e.g. no CurrentUser store in a
                // service context, or cert stores unsupported on the host) — try the next location.
            }
        }

        throw new InvalidOperationException(
            $"Certificate with thumbprint '{normalized}' was not found in LocalMachine\\My or CurrentUser\\My. " +
            "Install the shared certificate (with private key) on every app server.");
    }

    private X509Certificate2 LoadFromKeyVault(IConfiguration config)
    {
        // In a real implementation, you would use Azure Key Vault client here
        // For now, throw an exception to indicate this needs implementation
        throw new NotImplementedException("Azure Key Vault certificate loading not implemented. Please implement based on your cloud provider.");
    }

    private X509Certificate2 GetDevelopmentCertificate(string type)
    {
        // Create a self-signed certificate for development
        // In production, use proper certificates from a CA
        var certificateName = $"CN=CollateralAppraisal-{type}-{Environment.MachineName}";
        
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            certificateName, 
            rsa, 
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);

        var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        
        _logger.LogDebug("Created development certificate: {Subject}", certificate.Subject);
        return certificate;
    }
}