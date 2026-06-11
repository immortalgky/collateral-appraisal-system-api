namespace Auth.Application.Services;

public interface ILdapAuthenticationService
{
    Task<LdapAuthResult> AuthenticateAsync(string username, string password);

    /// <summary>
    /// Looks up a directory user by username and returns their attributes, WITHOUT validating a
    /// password. Binds as the app's service/integrated identity. Returns null if not found or on error.
    /// Used to pre-fill the user-creation screen.
    /// </summary>
    Task<LdapUserInfo?> GetUserInfoAsync(string username);
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
