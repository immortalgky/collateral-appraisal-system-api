using System.Security.Claims;
using System.Text.Encodings.Web;
using Auth.Infrastructure.Session;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Integration.Auth;

public class BypassAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "test-user"),
            new Claim("permissions", "request:read"),
            new Claim("permissions", "request:write"),
            new Claim("permissions", "document:read"),
            new Claim("permissions", "document:write"),
            new Claim("permissions", "FAILED_MESSAGE_VIEW"),
            new Claim("permissions", "FAILED_MESSAGE_MANAGE"),
            new Claim("permissions", "HISTORY_SEARCH_VIEW"),
        };
        // Plays a signed-in browser user, so it carries the cookie-session identity type the "Spa" policy accepts.
        var identity = new ClaimsIdentity(claims, AuthSchemes.AppCookie);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
