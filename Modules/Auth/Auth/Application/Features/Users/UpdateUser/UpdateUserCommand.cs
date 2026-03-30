namespace Auth.Application.Features.Users.UpdateUser;

public record UpdateUserCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string? Position,
    string? Department,
    Guid? CompanyId) : ICommand;
