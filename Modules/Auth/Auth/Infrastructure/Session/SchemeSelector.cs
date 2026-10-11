using Microsoft.AspNetCore.Http;
using OpenIddict.Validation.AspNetCore;
using Shared.Identity;

namespace Auth.Infrastructure.Session;

/// <summary>
/// Which authentication scheme serves a request: the dev header (non-production only), then an
/// <c>Authorization: Bearer</c> token (machine clients), then the app session cookie. One predicate, used by the
/// policy scheme and by the CSRF check, so the two can never disagree about whose request this is.
/// </summary>
public static class SchemeSelector
{
    public static string Select(HttpRequest request, bool allowDevBypass)
    {
        if (allowDevBypass && request.Headers.ContainsKey(DevAuthenticationHandler.DevHeaderName))
            return DevAuthenticationHandler.SchemeName;

        if (request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;

        return AuthSchemes.AppCookie;
    }
}
