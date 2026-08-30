namespace Auth.Domain.Auth.Features.Me;

public record MeResponse(
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
    bool MustChangePassword
);
