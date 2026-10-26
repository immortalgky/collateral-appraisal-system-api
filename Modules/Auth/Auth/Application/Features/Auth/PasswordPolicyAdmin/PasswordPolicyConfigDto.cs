namespace Auth.Application.Features.Auth.PasswordPolicyAdmin;

/// <summary>Full password-policy configuration for the admin maintenance screen.</summary>
public record PasswordPolicyConfigDto(
    int RequiredLength,
    bool RequireDigit,
    bool RequireLowercase,
    bool RequireUppercase,
    bool RequireNonAlphanumeric,
    int RequiredUniqueChars,
    int ExpiryDays,
    int HistoryCount,
    string Blocklist,
    bool LockoutEnabled,
    int MaxFailedAccessAttempts,
    int LockoutMinutes,
    /// <summary>Longest access window an admin may open on a temporary-access account, in hours.</summary>
    int MaxAccessWindowHours,
    /// <summary>External users (company accounts) must use an authenticator code to sign in.</summary>
    bool RequireTwoFactorForExternal,
    /// <summary>Hours a device that passed the code check skips it. 0 = ask every time.</summary>
    int TwoFactorRememberHours);
