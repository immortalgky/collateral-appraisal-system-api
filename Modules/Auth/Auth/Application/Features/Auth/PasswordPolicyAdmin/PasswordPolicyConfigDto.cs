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
    int LockoutMinutes);
