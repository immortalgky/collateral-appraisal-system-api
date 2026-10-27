using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Auth.Application.Services;

/// <summary>What a sealed passkey state carries: the options the browser was given, and when.</summary>
public sealed record PasskeyState(string Options, DateTime IssuedAt);

/// <summary>
/// Seals a WebAuthn challenge (with its options) into a short-lived opaque string the browser carries to the next request,
/// in place of a server-side store: any node can open what another sealed, and nothing has to be cleaned up. The purpose
/// binds a state to what it is for (<c>assert</c>, or <c>register:{userId}</c>), so one user's registration state is
/// useless to another and a sign-in state cannot finish a registration.
/// </summary>
public sealed class PasskeyStateSealer(IDataProtectionProvider dataProtection)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private const string Root = "Auth.Passkey.v1";

    public const string AssertPurpose = "assert";
    public static string RegisterPurpose(Guid userId) => $"register:{userId:N}";

    private ITimeLimitedDataProtector Protector(string purpose) =>
        dataProtection.CreateProtector(Root, purpose).ToTimeLimitedDataProtector();

    public string Seal(string purpose, string optionsJson, DateTime issuedAt, TimeSpan? lifetime = null) =>
        Protector(purpose).Protect(JsonSerializer.Serialize(new PasskeyState(optionsJson, issuedAt)), lifetime ?? Lifetime);

    /// <summary>The state, or null when it is missing, tampered with, expired, or sealed for another purpose or user.</summary>
    public PasskeyState? Unseal(string purpose, string? sealedState)
    {
        if (string.IsNullOrEmpty(sealedState)) return null;
        try
        {
            return JsonSerializer.Deserialize<PasskeyState>(Protector(purpose).Unprotect(sealedState));
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException)
        {
            return null;
        }
    }
}
