namespace Auth.Auth.Features.RegisterUser;

public record RegisterUserCommand(
    string Username,
    string Password,
    string Email,
    List<Guid> Permissions,
    List<Guid> Roles
) : ICommand<RegisterUserResult>;
