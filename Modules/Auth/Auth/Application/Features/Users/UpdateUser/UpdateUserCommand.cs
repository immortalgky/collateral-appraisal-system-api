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
    string? AoCode = null,
    // Bank staff employee id; only persisted for bank users (CompanyId == null).
    string? EmployeeId = null,
    // null = leave the temporary-access flag unchanged. Turning it on closes the account until an
    // admin opens a window; turning it off clears the expiry so the account is ordinary again.
    bool? IsTemporaryAccess = null) : ICommand;
