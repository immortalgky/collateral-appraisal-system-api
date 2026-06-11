namespace Auth.Application.Features.Auth.PasswordPolicyAdmin.UpdatePasswordPolicyConfig;

public record UpdatePasswordPolicyConfigCommand(
    int RequiredLength,
    bool RequireDigit,
    bool RequireLowercase,
    bool RequireUppercase,
    bool RequireNonAlphanumeric,
    int RequiredUniqueChars,
    int ExpiryDays,
    int HistoryCount,
    string? Blocklist,
    bool LockoutEnabled,
    int MaxFailedAccessAttempts,
    int LockoutMinutes) : ICommand;
