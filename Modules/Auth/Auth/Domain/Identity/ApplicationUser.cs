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
}
