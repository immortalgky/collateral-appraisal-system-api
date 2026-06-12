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

    /// <summary>
    /// Opens a connection and binds as the service/integrated identity — a real round-trip to the
    /// directory, with no user/password validation or search. Throws when the server is unreachable
    /// or the bind is rejected. Used by the LDAP health check to probe connectivity.
    /// </summary>
    Task CheckConnectionAsync(CancellationToken cancellationToken = default);
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
