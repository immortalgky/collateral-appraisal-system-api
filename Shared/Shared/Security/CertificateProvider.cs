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