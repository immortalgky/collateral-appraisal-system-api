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
    int LockoutMinutes,
    // Optional: the command binds straight from the request body, and a client that predates this
    // field must not save a 0 over the stored cap (nor be rejected for omitting it). Null = leave
    // whatever the admin set last time alone.
    int? MaxAccessWindowHours = null) : ICommand;
