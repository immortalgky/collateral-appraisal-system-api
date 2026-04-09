namespace Auth.Application.Features.Users.CreateUser;

public record CreateUserRequest(
    string Username,
    string Password,
    string Email,
    string FirstName,
    string LastName,
    string? Position,
    string? Department,
    Guid? CompanyId,
    List<Guid> Roles
);
