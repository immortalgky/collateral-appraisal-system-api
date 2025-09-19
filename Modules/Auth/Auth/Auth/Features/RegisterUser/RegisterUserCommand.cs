namespace Auth.Auth.Features.RegisterUser;

public record RegisterUserCommand(
    string Username,
    string Password,
    string Email,
    List<string> Permissions
) : ICommand<RegisterUserResult>;
