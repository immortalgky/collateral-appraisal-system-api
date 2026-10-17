namespace Auth.Domain.Passkeys;

/// <summary>
/// A WebAuthn credential a user signs in with. Columns follow ASP.NET Identity 10's <c>IdentityPasskeyData</c> so a later
/// move to its built-in store is a rename, not a redesign. The private key never leaves the user's device; only the
/// public key is kept here.
/// </summary>
public class UserPasskey
{
    public const int MaxPerUser = 10;
    public const int MaxNameLength = 100;

    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>The authenticator's credential id. Unique across all users: it is what an assertion is looked up by.</summary>
    public byte[] CredentialId { get; set; } = [];

    /// <summary>The COSE public key, as the authenticator reported it at registration.</summary>
    public byte[] PublicKey { get; set; } = [];

    /// <summary>What the user calls it ("Work laptop"). Shown to them and to admins; never used for lookup.</summary>
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    /// <summary>The last successful sign-in with it. Null until first used. Also what makes an assertion single-use.</summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>The authenticator's signature counter. Always 0 for synced passkeys, which do not count.</summary>
    public uint SignCount { get; set; }

    /// <summary>Comma-separated <c>AuthenticatorTransport</c> names the browser reported, for the allow-list of a later sign-in.</summary>
    public string? Transports { get; set; }

    public Guid AaGuid { get; set; }

    /// <summary>The credential can be synced to a cloud account (the authenticator's BE flag).</summary>
    public bool IsBackupEligible { get; set; }

    /// <summary>It is synced right now (the BS flag, refreshed at each sign-in). This is what the UI calls "synced".</summary>
    public bool IsBackedUp { get; set; }
}
