using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Integration.Auth;

/// <summary>
/// Same shape as <see cref="BypassAuthenticationHandler"/> but with NO "permissions" claims — used to
/// prove a policy-gated endpoint (e.g. FAILED_MESSAGE_VIEW) actually rejects an authenticated user who
/// simply lacks the permission, as opposed to only ever testing the happy path.
/// </summary>
public class NoPermissionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "no-permission-user")], Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
