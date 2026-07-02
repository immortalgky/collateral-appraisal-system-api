namespace Auth.Application.Features.Users.UpdateUser;

public record UpdateUserCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? Position,
    string? Department,
    Guid? CompanyId,
    // null = leave AuthSource unchanged (see UpdateUserRequest).
    string? AuthSource = null,
    // Bank-internal officer code; only persisted for bank users (CompanyId == null).
    string? AoCode = null) : ICommand;
