using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Shared.Configuration;
using Shared.Security;

namespace Shared.Tests.Configuration;

public class EncryptedConfigurationExtensionsTests
{
    private static X509Certificate2 CreateTestCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=EncryptedConfigTests", rsa,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }

    private static ConfigurationManager BuildConfig(IEnumerable<KeyValuePair<string, string?>> values)
    {
        var manager = new ConfigurationManager();
        ((IConfigurationBuilder)manager).AddInMemoryCollection(values);
        return manager;
    }

    [Fact]
    public void Decrypts_an_ENC_value_so_consumers_read_plaintext()
    {
        using var cert = CreateTestCertificate();
        var connString = "Server=sql01;Database=CollateralAppraisal;User Id=app;Password=S3cr3t!;";
        var encrypted = SecretProtector.Protect(connString, cert);

        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = encrypted,
            ["Serilog:MinimumLevel:Default"] = "Information", // untouched plaintext
        });

        config.AddDecryptedSecrets(cert);

        config.GetConnectionString("Database").Should().Be(connString);
        config["Serilog:MinimumLevel:Default"].Should().Be("Information");
    }

    [Fact]
    public void Is_a_no_op_when_nothing_is_encrypted()
    {
        using var cert = CreateTestCertificate();
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = "Server=localhost;",
        });

        config.AddDecryptedSecrets(cert);

        config.GetConnectionString("Database").Should().Be("Server=localhost;");
    }

    [Fact]
    public void Throws_when_encrypted_values_exist_but_no_thumbprint_is_configured()
    {
        using var cert = CreateTestCertificate();
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Mail:Password"] = SecretProtector.Protect("pw", cert),
        });

        // Public overload (no cert) resolves the thumbprint from config — absent here.
        Action act = () => config.AddDecryptedSecrets();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*no certificate*thumbprint*");
    }

    [Fact]
    public void Wraps_a_decrypt_failure_with_the_key_name_but_not_the_value()
    {
        using var cert = CreateTestCertificate();
        using var otherCert = CreateTestCertificate(); // different key pair → cannot decrypt

        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Mail:Password"] = SecretProtector.Protect("super-secret", cert),
        });

        Action act = () => config.AddDecryptedSecrets(otherCert);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("Mail:Password").And.NotContain("super-secret");
    }
}
