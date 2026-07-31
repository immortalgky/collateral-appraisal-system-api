using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Shared.Security;

/// <summary>
/// Encrypts / decrypts individual configuration secret values with an X509 certificate, so that
/// passwords never sit in plaintext in <c>appsettings.Production.json</c>.
///
/// <para>
/// Uses hybrid ("envelope") encryption rather than raw RSA: RSA-2048 OAEP can only encrypt
/// ~190 bytes, which a long connection string exceeds. A fresh random AES-256-GCM key encrypts
/// the value (any length), and only that small key is wrapped with the certificate's RSA public
/// key. Decryption needs the certificate's private key, which lives only on the app servers'
/// <c>LocalMachine\My</c> store.
/// </para>
///
/// <para>
/// Wire format of a protected value: <c>ENC:v1:BASE64(payload)</c> where <c>payload</c> is
/// <c>[2-byte wrappedKeyLen][wrappedKey][12-byte nonce][16-byte tag][ciphertext]</c>.
/// The same code encrypts (via the ops tool) and decrypts (at app startup), so the two can
/// never drift.
/// </para>
/// </summary>
public static class SecretProtector
{
    /// <summary>Prefix marking a configuration value as protected by this class.</summary>
    public const string Prefix = "ENC:v1:";

    private const int AesKeyBytes = 32;   // AES-256
    private const int NonceBytes = 12;    // AES-GCM standard nonce
    private const int TagBytes = 16;      // AES-GCM tag

    /// <summary>True if <paramref name="value"/> is an <c>ENC:v1:</c> protected value.</summary>
    public static bool IsProtected(string? value) =>
        value is not null && value.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> and returns an <c>ENC:v1:...</c> string.
    /// Only the certificate's public key is required.
    /// </summary>
    public static string Protect(string plaintext, X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentNullException.ThrowIfNull(certificate);

        using var rsa = certificate.GetRSAPublicKey()
            ?? throw new InvalidOperationException("Certificate has no RSA public key.");

        var aesKey = RandomNumberGenerator.GetBytes(AesKeyBytes);
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagBytes];

        using (var aes = new AesGcm(aesKey, TagBytes))
        {
            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);
        }

        // Wrap the AES key with the cert's public key, then clear it from memory.
        var wrappedKey = rsa.Encrypt(aesKey, RSAEncryptionPadding.OaepSHA256);
        CryptographicOperations.ZeroMemory(aesKey.AsSpan());

        using var payload = new MemoryStream();
        using (var writer = new BinaryWriter(payload, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((ushort)wrappedKey.Length);
            writer.Write(wrappedKey);
            writer.Write(nonce);
            writer.Write(tag);
            writer.Write(ciphertext);
        }

        return Prefix + Convert.ToBase64String(payload.ToArray());
    }

    /// <summary>
    /// Decrypts an <c>ENC:v1:...</c> string produced by <see cref="Protect"/>.
    /// Requires the certificate's private key. Throws on a tampered or malformed payload.
    /// </summary>
    public static string Unprotect(string protectedValue, X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(protectedValue);
        ArgumentNullException.ThrowIfNull(certificate);

        if (!IsProtected(protectedValue))
            throw new FormatException($"Value is not an {Prefix} protected value.");

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(protectedValue[Prefix.Length..]);
        }
        catch (FormatException ex)
        {
            throw new FormatException("Protected value is not valid base64.", ex);
        }

        using var reader = new BinaryReader(new MemoryStream(payload));
        int wrappedKeyLength = reader.ReadUInt16();
        var wrappedKey = reader.ReadBytes(wrappedKeyLength);
        var nonce = reader.ReadBytes(NonceBytes);
        var tag = reader.ReadBytes(TagBytes);
        var ciphertext = reader.ReadBytes(payload.Length - 2 - wrappedKeyLength - NonceBytes - TagBytes);

        if (wrappedKey.Length != wrappedKeyLength || nonce.Length != NonceBytes || tag.Length != TagBytes)
            throw new FormatException("Protected value is truncated or malformed.");

        using var rsa = certificate.GetRSAPrivateKey()
            ?? throw new InvalidOperationException(
                "Certificate has no accessible RSA private key; it is required to decrypt secrets.");

        var aesKey = rsa.Decrypt(wrappedKey, RSAEncryptionPadding.OaepSHA256);
        try
        {
            var plaintextBytes = new byte[ciphertext.Length];
            using var aes = new AesGcm(aesKey, TagBytes);
            aes.Decrypt(nonce, ciphertext, tag, plaintextBytes); // throws on tamper (auth tag mismatch)
            return Encoding.UTF8.GetString(plaintextBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(aesKey.AsSpan());
        }
    }
}
