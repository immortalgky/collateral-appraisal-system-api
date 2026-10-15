using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Integration.Auth;

/// <summary>
/// An authenticated user with FAILED_MESSAGE_VIEW but NOT FAILED_MESSAGE_MANAGE —
/// proves a write endpoint (e.g. POST /admin/failed-messages/retry) rejects a viewer, not just an
/// unauthenticated/no-permission caller (see <see cref="NoPermissionAuthenticationHandler"/>).
/// </summary>
public class ViewOnlyFailedMessagePermissionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "view-only-user"),
            new Claim("permissions", "FAILED_MESSAGE_VIEW"),
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
