using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Shared.Identity;

public class DevAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DevBypass";
    public const string DevHeaderName = "X-Dev-Auth";
    public const string DevHeaderValue = "dev-bypass";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(DevHeaderName, out var headerValue)
            || headerValue != DevHeaderValue)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var devId = Guid.Empty.ToString();

        var claims = new[]
        {
            new Claim("sub", devId),
            new Claim("name", "dev-user"),
            new Claim("company_id", devId),
            // Permissions
            new Claim("permissions", "auth:read"),
            new Claim("permissions", "auth:write"),
            new Claim("permissions", "document:read"),
            new Claim("permissions", "document:write"),
            new Claim("permissions", "notification:read"),
            new Claim("permissions", "notification:write"),
            new Claim("permissions", "request:read"),
            new Claim("permissions", "request:write"),
            new Claim("permissions", "PERMISSION_MANAGE"),
            new Claim("permissions", "ROLE_MANAGE"),
            new Claim("permissions", "GROUP_MANAGE"),
            new Claim("permissions", "USER_MANAGE"),
            new Claim("permissions", "USER_CHANGE_PASSWORD"),
            new Claim("permissions", "USER_RESET_PASSWORD"),
            // Roles
            new Claim("roles", "Admin"),
            // Scopes
            new Claim("scope", "appraisal.read"),
            new Claim("scope", "request.write"),
            new Claim("scope", "document.read"),
            new Claim("scope", "document.write"),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
