namespace Auth.Application.Features.Users.UpdateUser;

public record UpdateUserRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Position,
    string? Department,
    Guid? CompanyId,
    // null = leave the account's authentication source unchanged. Only send a value to deliberately
    // switch auth mode — omitting it must NOT silently flip an LDAP user to Local.
    string? AuthSource = null,
    // Bank-internal officer code; only persisted for bank users (CompanyId == null).
    string? AoCode = null);
