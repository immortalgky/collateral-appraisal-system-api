namespace Auth.Application.Features.Users.UpdateUser;

public record UpdateUserRequest(
    string FirstName,
    string LastName,
    string? Position,
    string? Department,
    Guid? CompanyId);
