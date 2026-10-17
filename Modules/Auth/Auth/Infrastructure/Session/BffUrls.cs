using Microsoft.AspNetCore.Http;

namespace Auth.Infrastructure.Session;

/// <summary>
/// The public addresses of the API and the SPA, as configured — never as the request reports them
/// (behind the F5 only the scheme is forwarded, so Host cannot be trusted to name the public URL).
/// </summary>
public sealed class BffUrls
{
    private readonly string[] _corsOrigins;

    /// <param name="appBaseUrl">The API's public base URL (<c>AppBaseUrl</c>), also the OpenIddict issuer.</param>
    /// <param name="corsOrigins"><c>Cors:AllowedOrigins</c> — the SPA origins.</param>
    /// <param name="idpPublicBaseUrl">
    /// <c>Auth:Idp:PublicBaseUrl</c> — where browsers reach the identity provider. Blank while the IdP runs inside
    /// this API (it is then the API's own public address); set once the IdP is split out.
    /// </param>
    public BffUrls(string appBaseUrl, IEnumerable<string> corsOrigins, string? idpPublicBaseUrl = null)
    {
        if (!Uri.TryCreate(appBaseUrl, UriKind.Absolute, out var baseUri))
            throw new InvalidOperationException(
                "AppBaseUrl must be set to the API's public base URL: it is the OpenIddict issuer and the OIDC redirect origin.");

        RequireRootPath(baseUri, "AppBaseUrl");
        PublicBase = baseUri.GetLeftPart(UriPartial.Authority);
        if (string.IsNullOrWhiteSpace(idpPublicBaseUrl))
            IdpPublicBase = PublicBase;
        else if (Uri.TryCreate(idpPublicBaseUrl, UriKind.Absolute, out var idpUri))
        {
            RequireRootPath(idpUri, "Auth:Idp:PublicBaseUrl");
            IdpPublicBase = idpUri.GetLeftPart(UriPartial.Authority);
        }
        else
            throw new InvalidOperationException("Auth:Idp:PublicBaseUrl must be an absolute URL, or empty.");
        _corsOrigins = corsOrigins
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Select(o => o.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    // Only scheme://host[:port] is used from these URLs, while the redirect URIs registered for the SPA client are
    // built from the whole AppBaseUrl and the __Host- cookies require Path=/. A sub-path would silently disagree.
    private static void RequireRootPath(Uri uri, string setting)
    {
        if (uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            throw new InvalidOperationException(
                $"{setting} must be a bare origin (scheme://host[:port]) with no path: the app is served from the root of its host. Got '{uri}'.");
    }

    /// <summary>Scheme + host + port of the API, no trailing slash.</summary>
    public string PublicBase { get; }

    /// <summary>
    /// Scheme + host + port at which browsers reach the identity provider: the authorize and end-session pages.
    /// The same as <see cref="PublicBase"/> until the IdP is split out of the API.
    /// </summary>
    public string IdpPublicBase { get; }

    /// <summary>The SPA origins (<c>Cors:AllowedOrigins</c>), without trailing slashes.</summary>
    public IReadOnlyList<string> CorsOrigins => _corsOrigins;

    /// <summary>Where the browser lands when nothing more specific is known: the first SPA origin.</summary>
    public string SpaHome => _corsOrigins.Length > 0 ? _corsOrigins[0] + "/" : "/";

    /// <summary>
    /// Whether a request's <c>Origin</c> may act on a cookie session: an SPA origin, the API itself, or the
    /// identity provider's own pages (its login form posts from there).
    /// </summary>
    public bool IsTrustedOrigin(string? origin) =>
        !string.IsNullOrEmpty(origin)
        && (string.Equals(origin.TrimEnd('/'), PublicBase, StringComparison.OrdinalIgnoreCase)
            || string.Equals(origin.TrimEnd('/'), IdpPublicBase, StringComparison.OrdinalIgnoreCase)
            || _corsOrigins.Contains(origin.TrimEnd('/'), StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Re-bases an identity-provider endpoint address (built from the internal Authority) onto the address
    /// browsers use to reach the IdP.
    /// </summary>
    public string ToPublic(string address) => IdpPublicBase + new Uri(address).PathAndQuery;

    /// <summary>
    /// Whether the request reached the API under the host and port of <see cref="PublicBase"/>. The sign-in callback
    /// (and so the session cookie) lands on <c>AppBaseUrl</c>; a request that came in on another host — the SPA's
    /// <c>VITE_API_URL</c> not equal to <c>AppBaseUrl</c> — would sign in on one host and keep calling another,
    /// where the cookie never arrives. A default port (443/80) may be written or omitted. A proxy's
    /// <c>X-Forwarded-Host</c>, when present, stands in for the Host it replaced.
    /// </summary>
    public bool RequestMatchesPublicHost(HttpRequest request)
    {
        var expected = new Uri(PublicBase);
        var forwarded = request.Headers["X-Forwarded-Host"].ToString();
        var host = forwarded.Length > 0 ? HostString.FromUriComponent(forwarded.Split(',')[0].Trim()) : request.Host;
        return string.Equals(host.Host, expected.Host, StringComparison.OrdinalIgnoreCase)
               && (host.Port ?? (expected.Scheme == Uri.UriSchemeHttps ? 443 : 80)) == expected.Port;
    }

    /// <summary>
    /// The post-login destination: <paramref name="returnUrl"/> when it is an absolute http(s) URL on one of
    /// the SPA origins, or a path on this API's own origin; otherwise <see cref="SpaHome"/>. An open redirect here would let a phishing link
    /// ride a genuine sign-in, so the origin is compared exactly, not by prefix.
    /// </summary>
    public string ResolveReturnUrl(string? returnUrl)
    {
        // A path on the API's own origin (a Hangfire dashboard URL that sent the browser through sign-in) is as
        // safe as it gets: it cannot leave this origin. "//host" and "/\host" are scheme-relative to browsers.
        if (returnUrl is { Length: > 0 } && returnUrl[0] == '/'
            && (returnUrl.Length == 1 || returnUrl[1] is not ('/' or '\\'))
            && !returnUrl.Any(char.IsControl))
            return returnUrl;

        if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri)
            && uri.Scheme is "https" or "http"
            && string.IsNullOrEmpty(uri.UserInfo)
            && _corsOrigins.Contains(uri.GetLeftPart(UriPartial.Authority), StringComparer.OrdinalIgnoreCase))
            return uri.AbsoluteUri;

        return SpaHome;
    }
}
