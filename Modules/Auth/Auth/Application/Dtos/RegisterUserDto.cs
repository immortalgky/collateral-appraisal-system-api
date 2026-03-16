namespace Auth.Application.Dtos;

public record RegisterUserDto(
    string Username,
    string Password,
    string Email,
    string FirstName,
    string LastName,
    string? AvatarUrl,
    string? Position,
    string? Department,
    Guid? CompanyId,
    List<RegisterUserPermissionDto> Permissions,
    List<Guid> Roles
);
