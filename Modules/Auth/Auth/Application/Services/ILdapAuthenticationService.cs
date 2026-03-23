namespace Auth.Application.Services;

public interface ILdapAuthenticationService
{
    Task<LdapAuthResult> AuthenticateAsync(string username, string password);
}

public record LdapAuthResult(bool Succeeded, LdapUserInfo? UserInfo = null, string? ErrorMessage = null);

public record LdapUserInfo(
    string Username,
    string? Email,
    string? FirstName,
    string? LastName,
    string? Department,
    string? Position,
    string? DistinguishedName);
