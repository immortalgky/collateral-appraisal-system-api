using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace Integration.Auth.Integration.Tests.Bff;

/// <summary>
/// A WebAuthn authenticator in software: ES256, attestation format "none", the answers a browser's
/// <c>credential.toJSON()</c> would carry. Enough for the server to verify a real registration and assertion; each knob
/// below makes it misbehave in one way.
/// </summary>
public sealed class SoftAuthenticator
{
    public string RpId { get; set; } = "localhost";
    public byte[] CredentialId { get; } = RandomNumberGenerator.GetBytes(32);
    public ECDsa Key { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    /// <summary>The user handle the credential was created with (the server's user id), set by <see cref="Create"/>.</summary>
    public byte[] UserHandle { get; private set; } = [];

    /// <summary>Whether a PIN or biometric was used. A real authenticator asked for it always sets this; turn it off to test the refusal.</summary>
    public bool UserVerified { get; set; } = true;

    /// <summary>A counting authenticator (a hardware key). False = counter stays 0, like a synced passkey.</summary>
    public bool Counts { get; set; } = true;

    public bool BackupEligible { get; set; }
    public bool BackedUp { get; set; }

    /// <summary>The COSE algorithm the public key claims. -7 is ES256; anything else is not something the server supports.</summary>
    public int CoseAlgorithm { get; set; } = -7;

    private uint _signCount;

    public static string B64(byte[] bytes) => WebEncoders.Base64UrlEncode(bytes);

    private static byte[] FromB64(string value) => WebEncoders.Base64UrlDecode(value);

    private byte Flags(bool attested) =>
        (byte)(0x01 | (UserVerified ? 0x04 : 0) | (BackupEligible ? 0x08 : 0) | (BackedUp ? 0x10 : 0) | (attested ? 0x40 : 0));

    private static byte[] ClientData(string type, string challengeB64, string origin) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type, challenge = challengeB64, origin, crossOrigin = false }));

    private static byte[] Counter(uint value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    private byte[] RpIdHash() => SHA256.HashData(Encoding.UTF8.GetBytes(RpId));

    /// <summary>The response to <c>navigator.credentials.create</c> for the given creation options (the JSON the server sent).</summary>
    public string Create(string optionsJson, string origin)
    {
        using var options = JsonDocument.Parse(optionsJson);
        var challenge = options.RootElement.GetProperty("challenge").GetString()!;
        UserHandle = FromB64(options.RootElement.GetProperty("user").GetProperty("id").GetString()!);

        var parameters = Key.ExportParameters(false);
        var cose = new CborWriter(CborConformanceMode.Ctap2Canonical);
        cose.WriteStartMap(5);
        cose.WriteInt32(1); cose.WriteInt32(2); // kty: EC2
        cose.WriteInt32(3); cose.WriteInt32(CoseAlgorithm); // alg: ES256 unless told otherwise
        cose.WriteInt32(-1); cose.WriteInt32(1); // crv: P-256
        cose.WriteInt32(-2); cose.WriteByteString(parameters.Q.X!);
        cose.WriteInt32(-3); cose.WriteByteString(parameters.Q.Y!);
        cose.WriteEndMap();

        var authData = RpIdHash().Concat([Flags(attested: true)]).Concat(Counter(_signCount)).Concat(new byte[16])
            .Concat([(byte)(CredentialId.Length >> 8), (byte)CredentialId.Length]).Concat(CredentialId).Concat(cose.Encode()).ToArray();

        var attestation = new CborWriter(CborConformanceMode.Ctap2Canonical);
        attestation.WriteStartMap(3);
        attestation.WriteTextString("fmt"); attestation.WriteTextString("none");
        attestation.WriteTextString("attStmt"); attestation.WriteStartMap(0); attestation.WriteEndMap();
        attestation.WriteTextString("authData"); attestation.WriteByteString(authData);
        attestation.WriteEndMap();

        return JsonSerializer.Serialize(new
        {
            id = B64(CredentialId),
            rawId = B64(CredentialId),
            type = "public-key",
            authenticatorAttachment = "platform",
            response = new
            {
                clientDataJSON = B64(ClientData("webauthn.create", challenge, origin)),
                attestationObject = B64(attestation.Encode()),
                transports = new[] { "internal" }
            },
            clientExtensionResults = new { }
        });
    }

    /// <summary>The response to <c>navigator.credentials.get</c> for the given request options.</summary>
    /// <param name="userHandle">What the authenticator reports as the credential's user; default the one it was created with.</param>
    public string Get(string optionsJson, string origin, byte[]? userHandle = null)
    {
        using var options = JsonDocument.Parse(optionsJson);
        var challenge = options.RootElement.GetProperty("challenge").GetString()!;
        var clientData = ClientData("webauthn.get", challenge, origin);

        if (Counts) _signCount++;
        var authData = RpIdHash().Concat([Flags(attested: false)]).Concat(Counter(Counts ? _signCount : 0)).ToArray();
        var signature = Key.SignData(authData.Concat(SHA256.HashData(clientData)).ToArray(), HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);

        return JsonSerializer.Serialize(new
        {
            id = B64(CredentialId),
            rawId = B64(CredentialId),
            type = "public-key",
            authenticatorAttachment = "platform",
            response = new
            {
                clientDataJSON = B64(clientData),
                authenticatorData = B64(authData),
                signature = B64(signature),
                userHandle = B64(userHandle ?? UserHandle)
            },
            clientExtensionResults = new { }
        });
    }
}
