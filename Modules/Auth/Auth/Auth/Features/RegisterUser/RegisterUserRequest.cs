namespace Auth.Auth.Features.RegisterUser;

public record RegisterUserRequest(
    string Username,
    string Password,
    string Email,
    List<RegisterUserPermissionDto> Permissions,
    List<Guid> Roles
);
