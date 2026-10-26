using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Identity;

namespace Auth.Infrastructure.Session;

/// <summary>
/// The test behind the "Spa" policy (also the default and fallback). An allowlist over every authenticated
/// identity: each must be the app cookie session, or the dev header where it is allowed. A token validated by
/// OpenIddict, or by any scheme added later, is authenticated but refused here until an endpoint names a policy
/// of its own; so is a principal that merges a cookie identity with such a token.
/// </summary>
public static class SpaSessionPolicy
{
    public static bool IsBrowserSession(AuthorizationHandlerContext ctx, bool allowDevBypass)
    {
        // A plain loop: this runs on nearly every SPA request. Unauthenticated identities are ignored.
        var authenticated = false;
        ClaimsIdentity? offender = null;
        foreach (var identity in ctx.User.Identities)
        {
            if (!identity.IsAuthenticated) continue;
            authenticated = true;
            if (identity.AuthenticationType != AuthSchemes.AppCookie
                && !(allowDevBypass && identity.AuthenticationType == DevAuthenticationHandler.SchemeName))
                offender ??= identity;
        }

        // An unauthenticated caller (junk or expired token) is already a 401; the assertion still runs after
        // RequireAuthenticatedUser fails, so say nothing about it here.
        if (!authenticated) return false;
        if (offender is null) return true;

        // Path only: no query string, no token, no headers. Both HttpContext-based resources (minimal API and MVC
        // under endpoint routing) land here; without one there is no logger to get, so the refusal is silent.
        var http = ctx.Resource as HttpContext;
        http?.RequestServices?.GetService<ILoggerFactory>()?.CreateLogger("Auth.SpaPolicy").LogWarning(
            "[AUTH-SPA-REFUSED] {ClientId} {Method} {Path}",
            offender.FindFirst("client_id")?.Value ?? offender.FindFirst("sub")?.Value ?? "(unknown)",
            http.Request.Method, http.Request.Path.ToUriComponent());
        return false;
    }
}
