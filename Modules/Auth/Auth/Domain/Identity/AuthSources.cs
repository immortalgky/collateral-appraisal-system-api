namespace Auth.Domain.Identity;

/// <summary>
/// Canonical authentication-source values and classification helpers. <see cref="ApplicationUser.AuthSource"/>
/// is a free-form persisted string, so password-lifecycle gates must classify it rather than compare a
/// literal: anything that is not explicitly LDAP is treated as a local-password account. This keeps
/// casing/whitespace/legacy-value drift (e.g. "local", " Local", null) from silently disabling password
/// expiry / history / reuse checks on accounts that still authenticate with a local password.
/// </summary>
public static class AuthSources
{
    public const string Local = "Local";
    public const string Ldap = "LDAP";

    /// <summary>True only when the account authenticates against AD/LDAP.</summary>
    public static bool IsLdap(string? source) =>
        string.Equals(source?.Trim(), Ldap, StringComparison.OrdinalIgnoreCase);

    /// <summary>Local-password account = anything not explicitly LDAP (incl. null/empty/legacy values).</summary>
    public static bool IsLocal(string? source) => !IsLdap(source);
}
