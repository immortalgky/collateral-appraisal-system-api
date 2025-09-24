namespace Auth.Auth.Features.RegisterUser;

public record RegisterUserCommand(
    string Username,
    string Password,
    string Email,
    List<RegisterUserPermissionDto> Permissions,
    List<Guid> Roles
) : ICommand<RegisterUserResult>;
