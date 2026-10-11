using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace Auth.Infrastructure.Session;

/// <summary>
/// CSRF and cross-site WebSocket defence for requests that rely on the app session cookie: Origin verification.
/// </summary>
/// <remarks>
/// SameSite=Lax is not enough here: SPA and API are same-site but cross-origin, and every other
/// <c>*.lhb.net</c> host or port on a shared SIT/UAT host is same-site too. So a cookie-authenticated request
/// that changes state (any method but GET/HEAD/OPTIONS/TRACE) or opens a WebSocket must carry an
/// <c>Origin</c> (falling back to the <c>Referer</c>'s origin) that is one of <c>Cors:AllowedOrigins</c> or the
/// API's own public origin; missing, <c>null</c> or foreign is a 403. The browser fills Origin in itself and a
/// page cannot forge it, which makes the check hold for every kind of request alike: SPA XHR, multipart
/// uploads, the Razor login form (its antiforgery still applies on top), the Hangfire dashboard's own posts,
/// SignalR negotiate and WebSocket upgrades — HTTP/1.1 <c>Upgrade</c> or HTTP/2 extended CONNECT.
/// No custom header is required or sent: Origin/Referer alone decides.
/// Requests without the cookie are not touched: a Bearer caller, the dev-bypass header, or no session at all
/// cannot be ridden by a third-party page. GET callbacks (<c>/signin-oidc</c>) are not state-changing here.
/// </remarks>
public sealed class CsrfProtectionMiddleware(
    RequestDelegate next,
    BffUrls urls,
    IOptions<BrowserSessionOptions> options,
    ILogger<CsrfProtectionMiddleware> logger,
    bool allowDevBypass)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        // HTTP/1.1 upgrades say so in the Upgrade header. Chromium opens WebSockets over HTTP/2 with extended
        // CONNECT (RFC 8441) instead: method CONNECT, no Upgrade header. Read both from the request, not from
        // context.WebSockets, which only knows once WebSocket middleware has run.
        var isWebSocket = request.Headers.Upgrade.ToString().Contains("websocket", StringComparison.OrdinalIgnoreCase)
                          || HttpMethods.IsConnect(request.Method)
                          || context.Features.Get<IHttpExtendedConnectFeature>()?.IsExtendedConnect == true;
        var isUnsafe = !(HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)
                         || HttpMethods.IsOptions(request.Method) || HttpMethods.IsTrace(request.Method));

        if ((isUnsafe || isWebSocket)
            && request.Cookies.ContainsKey(options.Value.AppCookieName)
            && SchemeSelector.Select(request, allowDevBypass) == AuthSchemes.AppCookie)
        {
            var origin = ReadOrigin(request);
            if (!urls.IsTrustedOrigin(origin))
            {
                logger.LogWarning("[AUTH-CSRF-REJECTED] {Method} {Path} origin={Origin}",
                    request.Method, request.Path.Value, origin ?? "(none)");
                await Results.Problem(title: "CSRF check failed", statusCode: StatusCodes.Status403Forbidden)
                    .ExecuteAsync(context);
                return;
            }
        }

        await next(context);
    }

    private static string? ReadOrigin(HttpRequest request)
    {
        var origin = request.Headers.Origin.ToString();
        if (!string.IsNullOrEmpty(origin)) return origin;

        return Uri.TryCreate(request.Headers.Referer.ToString(), UriKind.Absolute, out var referer)
            ? referer.GetLeftPart(UriPartial.Authority)
            : null;
    }
}
