namespace Auth.Application.Services;

/// <summary>
/// What Active Directory itself says about an account, for sign-ins that never send the AD password (passkeys). A password
/// bind is refused by AD for a disabled or expired account, a locked-out one, and one whose password has expired or must be
/// changed; a passkey sign-in has to refuse the same accounts, or it would be a way around what AD enforces.
/// <para>
/// "Password expired or must be changed" is one bit, UF_PASSWORD_EXPIRED of <c>msDS-User-Account-Control-Computed</c>. AD derives it
/// on read (MS-ADTS 3.1.1.4.5.17): set when <c>pwdLastSet</c> is 0 or older than the maximum age, but never for an account with
/// DONT_EXPIRE_PASSWORD (or a smart-card, trust or similar account). That is exactly when AD refuses the password, so
/// <c>pwdLastSet</c> is not read separately: doing so would refuse accounts AD lets in.
/// </para>
/// </summary>
public static class AdAccountStatus
{
    public const string UserAccountControlAttribute = "userAccountControl";
    public const string AccountExpiresAttribute = "accountExpires";
    public const string ComputedControlAttribute = "msDS-User-Account-Control-Computed";

    /// <summary>ACCOUNTDISABLE, in <c>userAccountControl</c>.</summary>
    private const int AccountDisabled = 0x2;

    /// <summary>UF_LOCKOUT, in the computed attribute: too many bad passwords, still inside the lockout.</summary>
    private const int Lockout = 0x10;

    /// <summary>UF_PASSWORD_EXPIRED, in the computed attribute: the password is past its maximum age, or must be changed at next logon.</summary>
    private const int PasswordExpired = 0x800000;

    /// <summary>The directory's "never": 0, or the largest 64-bit value.</summary>
    private const long Never = long.MaxValue;

    public static bool IsDisabled(int userAccountControl) => (userAccountControl & AccountDisabled) != 0;

    public static bool IsLockedOut(int computedControl) => (computedControl & Lockout) != 0;

    public static bool IsPasswordExpired(int computedControl) => (computedControl & PasswordExpired) != 0;

    /// <param name="accountExpires">A Windows FILETIME (100 ns ticks since 1601, UTC): 0 or <c>long.MaxValue</c> = never.</param>
    public static bool IsExpired(long accountExpires, DateTime utcNow)
    {
        if (accountExpires is 0 or Never) return false;
        if (accountExpires < 0) return true; // not a valid FILETIME: fail closed
        if (accountExpires > DateTime.MaxValue.ToFileTimeUtc()) return false; // beyond year 9999: as good as never
        return DateTime.FromFileTimeUtc(accountExpires) <= utcNow;
    }

    public const string Unavailable = "AdUnavailable";
    public const string AccountDisabledReason = "AdDisabled";
    public const string LockedOutReason = "AdLockedOut";
    public const string PasswordExpiredReason = "AdPasswordExpired";

    /// <summary>
    /// Why this directory entry must not sign in, or null if it may. <see cref="Unavailable"/> when the lookup failed or the
    /// directory did not return one of the attributes this decision rests on: not knowing is a refusal.
    /// </summary>
    public static string? RefusalReason(LdapUserInfo? info, DateTime utcNow)
    {
        if (info is not { UserAccountControl: { } control, AccountExpires: { } expires, UserAccountControlComputed: { } computed })
            return Unavailable;

        if (IsDisabled(control) || IsExpired(expires, utcNow)) return AccountDisabledReason;
        if (IsLockedOut(computed)) return LockedOutReason;
        if (IsPasswordExpired(computed)) return PasswordExpiredReason;
        return null;
    }
}
