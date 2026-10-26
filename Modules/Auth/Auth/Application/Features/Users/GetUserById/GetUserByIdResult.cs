using Auth.Application.Features.Auth.Passkeys;

namespace Auth.Application.Features.Users.GetUserById;

public record UserGroupDto(Guid Id, string Name, string Scope);

public record UserTeamDto(Guid Id, string Name, string Scope);

public record UserPermissionDto(Guid PermissionId, string PermissionCode, bool IsGranted);

public record UserRoleDto(Guid Id, string Name, string? Scope);

public record GetUserByIdResult(
    Guid Id,
    string Username,
    string FirstName,
    string LastName,
    string? Email,
    string? PhoneNumber,
    string? AvatarUrl,
    string? Position,
    string? Department,
    // Bank-internal officer code (joins to auth.Officers); null for company users.
    string? AoCode,
    // Bank staff employee id; null for company users.
    string? EmployeeId,
    Guid? CompanyId,
    string? CompanyName,
    // Thai name; null when the company has none. The client picks by its own locale.
    string? CompanyNameLocal,
    string AuthSource,
    bool IsActive,
    bool IsLocked,
    // Ad-hoc account usable only inside an admin-opened window; AccessExpiresAt is when that window ends.
    bool IsTemporaryAccess,
    DateTime? AccessExpiresAt,
    DateTime? LastLoginAt,
    bool TwoFactorEnabled,
    // The passkeys the user signed in with; an admin can remove them all (DELETE /auth/users/{id}/passkeys).
    List<PasskeyDto> Passkeys,
    List<UserRoleDto> Roles,
    List<UserGroupDto> Groups,
    List<UserTeamDto> Teams,
    List<UserPermissionDto> Permissions);
