namespace Auth.Domain.Configuration;

/// <summary>
/// The single, admin-maintained password policy for the system. Exactly one row exists.
/// Read through <c>IPasswordPolicyProvider</c> (cached) and enforced by <c>DbPasswordValidator</c>
/// so edits take effect without an application restart (lockout values are the exception — see the
/// admin screen note).
/// </summary>
public class PasswordPolicy
{
    public Guid Id { get; private set; }

    // ── Complexity (mirror ASP.NET Identity PasswordOptions) ────────────────────
    public int RequiredLength { get; private set; }
    public bool RequireDigit { get; private set; }
    public bool RequireLowercase { get; private set; }
    public bool RequireUppercase { get; private set; }
    public bool RequireNonAlphanumeric { get; private set; }
    public int RequiredUniqueChars { get; private set; }

    // ── Lifecycle ───────────────────────────────────────────────────────────────
    /// <summary>Days until a password expires. 0 = never expires.</summary>
    public int ExpiryDays { get; private set; }

    /// <summary>How many previous passwords cannot be reused. 0 = no history check.</summary>
    public int HistoryCount { get; private set; }

    /// <summary>Newline-delimited list of forbidden passwords (case-insensitive).</summary>
    public string Blocklist { get; private set; } = string.Empty;

    // ── Lockout (applied at startup — see admin screen) ─────────────────────────
    /// <summary>Master switch — whether failed-attempt lockout is enforced at all.</summary>
    public bool LockoutEnabled { get; private set; }

    public int MaxFailedAccessAttempts { get; private set; }

    /// <summary>Lockout duration in minutes. 0 = permanent until admin unlock.</summary>
    public int LockoutMinutes { get; private set; }

    // Required by EF Core
    private PasswordPolicy()
    {
    }

    /// <summary>The default policy — matches the rules that were previously hardcoded in AuthModule.</summary>
    public static PasswordPolicy CreateDefault() => new()
    {
        Id = Guid.CreateVersion7(),
        RequiredLength = 8,
        RequireDigit = true,
        RequireLowercase = true,
        RequireUppercase = true,
        RequireNonAlphanumeric = true,
        RequiredUniqueChars = 1,
        ExpiryDays = 0,
        HistoryCount = 0,
        Blocklist = string.Empty,
        LockoutEnabled = true,
        MaxFailedAccessAttempts = 5,
        LockoutMinutes = 0
    };

    public void Update(
        int requiredLength,
        bool requireDigit,
        bool requireLowercase,
        bool requireUppercase,
        bool requireNonAlphanumeric,
        int requiredUniqueChars,
        int expiryDays,
        int historyCount,
        string? blocklist,
        bool lockoutEnabled,
        int maxFailedAccessAttempts,
        int lockoutMinutes)
    {
        RequiredLength = Math.Clamp(requiredLength, 1, 128);
        RequireDigit = requireDigit;
        RequireLowercase = requireLowercase;
        RequireUppercase = requireUppercase;
        RequireNonAlphanumeric = requireNonAlphanumeric;
        RequiredUniqueChars = Math.Clamp(requiredUniqueChars, 0, 128);
        ExpiryDays = Math.Max(0, expiryDays);
        HistoryCount = Math.Clamp(historyCount, 0, 50);
        Blocklist = (blocklist ?? string.Empty).Trim();
        LockoutEnabled = lockoutEnabled;
        MaxFailedAccessAttempts = Math.Clamp(maxFailedAccessAttempts, 1, 100);
        LockoutMinutes = Math.Max(0, lockoutMinutes);
    }
}
