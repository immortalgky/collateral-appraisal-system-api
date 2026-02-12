namespace Auth.Domain.Auth.Features.RegisterUser;

public record RegisterUserRequest(
    string Username,
    string Password,
    string Email,
    string FirstName,
    string LastName,
    string? AvatarUrl,
    string? Position,
    string? Department,
    List<RegisterUserPermissionDto> Permissions,
    List<Guid> Roles
);
