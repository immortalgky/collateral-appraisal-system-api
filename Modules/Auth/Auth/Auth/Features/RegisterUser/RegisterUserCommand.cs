namespace Auth.Auth.Features.RegisterUser;

public record RegisterUserCommand(
    string Username,
    string Password,
    string Email,
    List<Guid> Permissions
) : ICommand<RegisterUserResult>;
