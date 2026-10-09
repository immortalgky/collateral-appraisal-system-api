using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Shared.Security;

namespace Shared.Tests.Security;

public class ColumnSecretCipherTests
{
    private static X509Certificate2 CreateTestCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=ColumnSecretCipherTests", rsa,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }

    [Fact]
    public void Protect_encrypts_and_Unprotect_round_trips()
    {
        using var cert = CreateTestCertificate();
        var cipher = new ColumnSecretCipher(cert, allowPlaintextWrites: false);

        var stored = cipher.Protect("hmac-secret");

        stored.Should().StartWith(SecretProtector.Prefix);
        cipher.Unprotect(stored).Should().Be("hmac-secret");
    }

    [Fact]
    public void Unprotect_passes_legacy_plaintext_through()
    {
        using var cert = CreateTestCertificate();

        new ColumnSecretCipher(cert, allowPlaintextWrites: false).Unprotect("legacy").Should().Be("legacy");
        new ColumnSecretCipher(null, allowPlaintextWrites: false).Unprotect("legacy").Should().Be("legacy");
    }

    [Fact]
    public void Without_a_certificate_Protect_stores_plaintext_only_when_allowed()
    {
        new ColumnSecretCipher(null, allowPlaintextWrites: true).Protect("dev").Should().Be("dev");

        var act = () => new ColumnSecretCipher(null, allowPlaintextWrites: false).Protect("prod");
        act.Should().Throw<InvalidOperationException>().WithMessage("*certificate*");
    }

    [Fact]
    public void Without_a_certificate_an_encrypted_value_cannot_be_read()
    {
        using var cert = CreateTestCertificate();
        var stored = new ColumnSecretCipher(cert, allowPlaintextWrites: false).Protect("x");

        var act = () => new ColumnSecretCipher(null, allowPlaintextWrites: true).Unprotect(stored);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_thumbprint_added_after_startup_is_picked_up_without_a_restart()
    {
        // No thumbprint at first: the cipher must not remember "no certificate" for the process lifetime.
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationManager();
        var environment = new Microsoft.Extensions.Hosting.Internal.HostingEnvironment { EnvironmentName = "Production" };
        var cipher = new ColumnSecretCipher(configuration, environment);

        var act = () => cipher.Protect("x");
        act.Should().Throw<InvalidOperationException>().WithMessage("*certificate*");

        // A bogus thumbprint now reaches the store lookup (and fails there) instead of the cached null.
        configuration["Secrets:CertificateThumbprint"] = "0000000000000000000000000000000000000000";
        act.Should().Throw<Exception>().Which.Message.Should().NotContain("no certificate is configured");
    }

    [Fact]
    public void A_rotated_thumbprint_is_used_for_new_secrets_without_a_restart()
    {
        using var oldCert = CreateTestCertificate();
        using var newCert = CreateTestCertificate();
        var current = oldCert;
        var cipher = new ColumnSecretCipher(() => current.Thumbprint, _ => current, allowPlaintextWrites: false);

        cipher.Protect("warm-up"); // caches the old cert
        current = newCert;
        var stored = cipher.Protect("rotated");

        SecretProtector.Unprotect(stored, newCert).Should().Be("rotated");
    }

    [Fact]
    public void A_decrypt_failure_surfaces_as_SecretCipherException()
    {
        using var writer = CreateTestCertificate();
        using var otherCert = CreateTestCertificate();
        var stored = new ColumnSecretCipher(writer, allowPlaintextWrites: false).Protect("x");

        var act = () => new ColumnSecretCipher(otherCert, allowPlaintextWrites: false).Unprotect(stored);

        act.Should().Throw<SecretCipherException>().WithInnerException<Exception>();
    }
}
