namespace Auth.Application.Features.Auth.GetPasswordPolicy;

public record GetPasswordPolicyResult(
    int RequiredLength,
    bool RequireDigit,
    bool RequireLowercase,
    bool RequireUppercase,
    bool RequireNonAlphanumeric,
    int RequiredUniqueChars);
