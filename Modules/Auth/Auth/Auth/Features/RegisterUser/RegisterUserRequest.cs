namespace Auth.Auth.Features.RegisterUser;

public record RegisterUserRequest(
    string Username,
    string Password,
    string Email,
    List<string> Permissions
);
