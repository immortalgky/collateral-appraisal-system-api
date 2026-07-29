using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Shared.Security;

namespace Shared.Tests.Security;

public class SecretProtectorTests
{
    /// <summary>A throwaway self-signed RSA cert (with private key) so tests need no cert store.</summary>
    private static X509Certificate2 CreateTestCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=SecretProtectorTests", rsa,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }

    [Theory]
    [InlineData("P@ssw0rd")]
    [InlineData("")]
    [InlineData("Server=sql01;Database=CollateralAppraisal;User Id=app;Password=S3cr3t!;TrustServerCertificate=True;")]
    public void Protect_then_Unprotect_round_trips(string plaintext)
    {
        using var cert = CreateTestCertificate();

        var encrypted = SecretProtector.Protect(plaintext, cert);
        var decrypted = SecretProtector.Unprotect(encrypted, cert);

        encrypted.Should().StartWith(SecretProtector.Prefix);
        decrypted.Should().Be(plaintext);
    }

    [Fact]
    public void Round_trips_a_value_longer_than_raw_RSA_can_hold()
    {
        // RSA-2048 OAEP-SHA256 maxes out near 190 bytes; 500 chars proves the AES envelope.
        var plaintext = new string('x', 500);
        using var cert = CreateTestCertificate();

        var decrypted = SecretProtector.Unprotect(SecretProtector.Protect(plaintext, cert), cert);

        decrypted.Should().Be(plaintext);
    }

    [Fact]
    public void Two_encryptions_of_the_same_value_differ_but_both_decrypt()
    {
        using var cert = CreateTestCertificate();

        var a = SecretProtector.Protect("same", cert);
        var b = SecretProtector.Protect("same", cert);

        a.Should().NotBe(b, "a fresh AES key + nonce is used each time");
        SecretProtector.Unprotect(a, cert).Should().Be("same");
        SecretProtector.Unprotect(b, cert).Should().Be("same");
    }

    [Fact]
    public void Unprotect_throws_on_a_tampered_payload()
    {
        using var cert = CreateTestCertificate();
        var encrypted = SecretProtector.Protect("P@ssw0rd", cert);

        // Flip a byte near the end (the ciphertext / auth tag region).
        var raw = Convert.FromBase64String(encrypted[SecretProtector.Prefix.Length..]);
        raw[^1] ^= 0xFF;
        var tampered = SecretProtector.Prefix + Convert.ToBase64String(raw);

        Action act = () => SecretProtector.Unprotect(tampered, cert);
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Unprotect_throws_when_value_is_not_protected()
    {
        using var cert = CreateTestCertificate();

        Action act = () => SecretProtector.Unprotect("plaintext", cert);

        act.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData("ENC:v1:abc", true)]
    [InlineData("plain", false)]
    [InlineData(null, false)]
    public void IsProtected_detects_the_prefix(string? value, bool expected)
    {
        SecretProtector.IsProtected(value).Should().Be(expected);
    }
}
