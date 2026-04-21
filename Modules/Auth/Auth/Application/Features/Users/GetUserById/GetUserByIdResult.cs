namespace Auth.Application.Features.Users.GetUserById;

public record UserGroupDto(Guid Id, string Name, string Scope);

public record UserPermissionDto(Guid PermissionId, string PermissionCode, bool IsGranted);

public record UserRoleDto(Guid Id, string Name, string? Scope);

public record GetUserByIdResult(
    Guid Id,
    string Username,
    string FirstName,
    string LastName,
    string? Email,
    string? AvatarUrl,
    string? Position,
    string? Department,
    Guid? CompanyId,
    string AuthSource,
    List<UserRoleDto> Roles,
    List<UserGroupDto> Groups,
    List<UserPermissionDto> Permissions);
