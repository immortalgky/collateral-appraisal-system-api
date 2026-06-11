using System.DirectoryServices.Protocols;
using System.Net;
using Auth.Application.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Auth.Application.Services;

public class LdapAuthenticationService(
    IOptions<LdapConfiguration> options,
    ILogger<LdapAuthenticationService> logger)
    : ILdapAuthenticationService
{
    private readonly LdapConfiguration _config = options.Value;

    public async Task<LdapAuthResult> AuthenticateAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return new LdapAuthResult(false, ErrorMessage: "Username and password are required.");

        try
        {
            // Step 1: Connect with service account and find user DN
            var userDn = await FindUserDnAsync(username);
            if (userDn is null)
                return new LdapAuthResult(false, ErrorMessage: "User not found in directory.");

            // Step 2: Bind with user credentials to validate password
            ValidateUserCredentials(username, userDn, password);

            // Step 3: Read user attributes with service account
            var userInfo = await ReadUserAttributesAsync(username, userDn);

            logger.LogInformation("LDAP authentication succeeded for user: {Username}", username);
            return new LdapAuthResult(true, UserInfo: userInfo);
        }
        catch (LdapException ex) when (ex.ErrorCode == 49) // Invalid credentials
        {
            logger.LogWarning("LDAP authentication failed for user: {Username} — invalid credentials", username);
            return new LdapAuthResult(false, ErrorMessage: "Invalid credentials.");
        }
        catch (LdapException ex)
        {
            logger.LogError(ex, "LDAP error during authentication for user: {Username}", username);
            return new LdapAuthResult(false, ErrorMessage: "Directory service error.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during LDAP authentication for user: {Username}", username);
            return new LdapAuthResult(false, ErrorMessage: "Authentication service unavailable.");
        }
    }

    public async Task<LdapUserInfo?> GetUserInfoAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return null;

        try
        {
            var userDn = await FindUserDnAsync(username);
            if (userDn is null)
            {
                logger.LogInformation("LDAP lookup found no user: {Username}", username);
                return null;
            }

            return await ReadUserAttributesAsync(username, userDn);
        }
        catch (LdapException ex)
        {
            logger.LogError(ex, "LDAP error during user lookup for: {Username}", username);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during LDAP user lookup for: {Username}", username);
            return null;
        }
    }

    private Task<string?> FindUserDnAsync(string username)
    {
        using var connection = CreateConnection();
        BindAsServiceIdentity(connection);

        var escapedUsername = EscapeLdapSearchFilter(username);
        var filter = string.Format(_config.SearchFilter, escapedUsername);

        var searchRequest = new SearchRequest(
            _config.BaseDn,
            filter,
            SearchScope.Subtree,
            "distinguishedName");

        var response = (SearchResponse)connection.SendRequest(searchRequest);

        if (response.Entries.Count == 0)
        {
            logger.LogWarning("LDAP user not found: {Username}", username);
            return Task.FromResult<string?>(null);
        }

        var dn = response.Entries[0].DistinguishedName;
        return Task.FromResult<string?>(dn);
    }

    private void ValidateUserCredentials(string username, string userDn, string password)
    {
        using var connection = CreateConnection();

        // Integrated mode uses Negotiate, which authenticates by sAMAccountName + domain — not a DN.
        // Service-account mode uses a simple bind (AuthType.Basic), which binds by the user's DN.
        if (_config.UseIntegratedAuth)
        {
            connection.Bind(new NetworkCredential(username, password, _config.Domain));
        }
        else
        {
            // Simple bind sends the password in the clear; without LDAPS the user's AD password
            // would cross the wire unencrypted. Refuse rather than leak it.
            if (!_config.UseSsl)
                throw new InvalidOperationException(
                    "LDAP service-account mode (UseIntegratedAuth=false) requires UseSsl=true; refusing to send the user password over an unencrypted connection.");

            connection.Bind(new NetworkCredential(userDn, password));
        }
    }

    private Task<LdapUserInfo> ReadUserAttributesAsync(string username, string userDn)
    {
        using var connection = CreateConnection();
        BindAsServiceIdentity(connection);

        var attrs = _config.Attributes;
        var searchRequest = new SearchRequest(
            userDn,
            "(objectClass=*)",
            SearchScope.Base,
            attrs.Username, attrs.Email, attrs.FirstName,
            attrs.LastName, attrs.Department, attrs.Position);

        var response = (SearchResponse)connection.SendRequest(searchRequest);
        if (response.Entries.Count == 0)
        {
            logger.LogWarning("LDAP attribute lookup returned no entries for DN: {UserDn}", userDn);
            throw new InvalidOperationException($"No LDAP entry found for DN '{userDn}'.");
        }

        var entry = response.Entries[0];

        var userInfo = new LdapUserInfo(
            Username: GetAttribute(entry, attrs.Username) ?? username,
            Email: GetAttribute(entry, attrs.Email),
            FirstName: GetAttribute(entry, attrs.FirstName),
            LastName: GetAttribute(entry, attrs.LastName),
            Department: GetAttribute(entry, attrs.Department),
            Position: GetAttribute(entry, attrs.Position),
            DistinguishedName: userDn);

        return Task.FromResult(userInfo);
    }

    private LdapConnection CreateConnection()
    {
        var identifier = new LdapDirectoryIdentifier(_config.Server, _config.Port);
        var connection = new LdapConnection(identifier)
        {
            Timeout = TimeSpan.FromSeconds(_config.ConnectionTimeoutSeconds)
        };

        connection.SessionOptions.ProtocolVersion = 3;

        // Integrated  -> Negotiate (Kerberos/NTLM) using the process/app-pool identity; seals traffic on plain LDAP.
        // Service acct -> Basic (simple bind), the only mode that accepts a distinguished name as the bind identity.
        connection.AuthType = _config.UseIntegratedAuth ? AuthType.Negotiate : AuthType.Basic;

        if (_config.UseSsl)
            connection.SessionOptions.SecureSocketLayer = true;

        return connection;
    }

    private void BindAsServiceIdentity(LdapConnection connection)
    {
        // Integrated: bind as the app's own Windows identity (no credentials supplied).
        // Service account: bind with the configured BindDn/BindPassword (simple bind).
        if (_config.UseIntegratedAuth)
            connection.Bind();
        else
            connection.Bind(new NetworkCredential(_config.BindDn, _config.BindPassword));
    }

    private static string? GetAttribute(SearchResultEntry entry, string attributeName)
    {
        var attr = entry.Attributes[attributeName];
        if (attr is null || attr.Count == 0)
            return null;
        return attr[0]?.ToString();
    }

    /// <summary>
    /// Escapes special characters in LDAP search filter values to prevent injection.
    /// See RFC 4515 Section 3.
    /// </summary>
    private static string EscapeLdapSearchFilter(string input)
    {
        var sb = new System.Text.StringBuilder(input.Length);
        foreach (var c in input)
        {
            switch (c)
            {
                case '\\': sb.Append("\\5c"); break;
                case '*': sb.Append("\\2a"); break;
                case '(': sb.Append("\\28"); break;
                case ')': sb.Append("\\29"); break;
                case '\0': sb.Append("\\00"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }
}
