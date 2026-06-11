namespace Auth.Application.Features.Users.LookupLdapUser;

public record LookupLdapUserResult(
    bool Found,
    string Username,
    string? Email,
    string? FirstName,
    string? LastName,
    string? Department,
    string? Position);
