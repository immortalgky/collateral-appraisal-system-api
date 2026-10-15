namespace Auth.Domain.Auth.Features.Me;

public record MeGroupDto(Guid GroupId, string GroupName, string Scope);

public record MeTeamDto(Guid TeamId, string TeamName, string Scope);

public record MeResult(
    Guid Id,
    string Username,
    string? Email,
    string FirstName,
    string LastName,
    string? AvatarUrl,
    string? Position,
    string? Department,
    string? AoCode,
    string? EmployeeId,
    Guid? CompanyId,
    string? CompanyName,
    string? CompanyNameLocal,
    string AuthSource,
    bool IsActive,
    DateTime? LastLoginAt,
    DateTime? PasswordChangedAt,
    List<string> Roles,
    List<string> Permissions,
    List<MeGroupDto> Groups,
    List<MeTeamDto> Teams,
    bool MustChangePassword,
    // End of a temporary-access window, server local time, whole seconds, no 'Z' — the value the
    // access_expires_at token claim carried. Null (omitted) for ordinary accounts.
    DateTime? AccessExpiresAt
);
