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
    int MaxAccessWindowHours);
