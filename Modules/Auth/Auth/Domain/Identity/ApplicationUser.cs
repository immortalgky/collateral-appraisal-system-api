namespace Auth.Domain.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Position { get; set; }
    public string? Department { get; set; }
    public Guid? CompanyId { get; set; }
    public string AuthSource { get; set; } = AuthSources.Local;
    public string? AoCode { get; set; }

    /// <summary>Bank staff employee id. Surfaced to the legacy (AS400) result feed as InternalValuerCode.
    /// Maintained manually or via LDAP sync (that population is a separate follow-up).</summary>
    public string? EmployeeId { get; set; }
    public List<UserPermission> Permissions { get; set; } = default!;

    /// <summary>Whether the account is allowed to sign in. Defaults to true.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Timestamp of the most recent successful login.</summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>When true the user must change their password before using the system.</summary>
    public bool MustChangePassword { get; set; } = false;

    /// <summary>When the local password was last set. Used to enforce password expiry. Null for legacy/LDAP accounts.</summary>
    public DateTime? PasswordChangedAt { get; set; }

    /// <summary>Technical account (break-glass admin, service account) rather than a real member of staff.
    /// System accounts are excluded from the user-facing reports — the Access Report, the user list and
    /// the auth audit log — so the bank's access matrix only shows actual people. The account keeps working
    /// normally; it is only hidden from those reads, and there is no way to opt back in through the API,
    /// so maintaining such an account has to happen in the database.</summary>
    public bool IsSystem { get; set; } = false;

    /// <summary>Ad-hoc account that is closed by default and only usable inside an access window an
    /// admin opens (see <see cref="AccessExpiresAt"/>). Local-password accounts only: opening a window
    /// rotates the password, which we cannot do for an AD-backed account. The flag is what gates the
    /// access-window endpoint — a normal staff account can never have its password rotated by it.</summary>
    public bool IsTemporaryAccess { get; set; } = false;

    /// <summary>End of the current access window. Null means "no expiry" — every ordinary account.
    /// A value in the past means the account is closed. Checked by <see cref="IsUsable"/>.</summary>
    public DateTime? AccessExpiresAt { get; set; }

    /// <summary>Whether the account may be used right now: active, and inside its access window when
    /// it has one. Every account-state gate (login, refresh, Hangfire dashboard) asks this single
    /// question so a window that has run out closes the same doors deactivation closes.</summary>
    public bool IsUsable(DateTime now) => IsActive && (AccessExpiresAt is null || AccessExpiresAt > now);
}
