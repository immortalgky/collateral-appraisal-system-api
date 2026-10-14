using System.Security.Claims;
using Auth.Application.Services;
using Auth.Domain.Sessions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Shared.Time;

namespace Auth.Infrastructure.Session;

/// <summary>
/// Event handlers of the app cookie and the OIDC handler. Counters for the in-app log viewer are the
/// <c>[AUTH-...]</c> log lines: there is no external metrics stack, dbo.Logs is the place to count them.
/// </summary>
public static class AppSessionEvents
{
    private const string LoggerCategory = "Auth.Session";

    /// <summary>Response header on a 401: <c>idle</c> or <c>ended</c>. Exposed through CORS so the SPA can read it.</summary>
    public const string SessionEndHeader = "X-Session-End";

    // ── App cookie ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs on every request that carries a live session: refuses accounts that are no longer usable and
    /// adds the claims the cookie does not hold (see <see cref="AuthorizationClaimsBuilder"/>).
    /// </summary>
    public static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var http = context.HttpContext;
        var services = http.RequestServices;

        if (!Guid.TryParse(context.Principal?.FindFirstValue("sub"), out var userId))
        {
            await EndSessionAsync(context, SessionEndReason.Revoked);
            return;
        }

        var now = services.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var builder = () => services.GetRequiredService<AuthorizationClaimsBuilder>();

        var fromCache = AuthorizationClaimsBuilder.TryGetCached(services.GetRequiredService<IMemoryCache>(), userId,
            out var snapshot);
        if (!fromCache)
            snapshot = await builder().ReloadAsync(userId, http.RequestAborted);
        else if (snapshot is { MustChangePassword: true })
            snapshot = await builder().RefreshMustChangePasswordAsync(userId, snapshot, http.RequestAborted);

        // Ending a session is terminal, so it never rests on a cached copy: an access window extended — or an
        // account reactivated — through the other node would otherwise be cut off up to a cache lifetime late. Ask
        // the database again, and only end the session if it still says so.
        if (fromCache && !snapshot!.IsUsable(now))
            snapshot = await builder().ReloadAsync(userId, http.RequestAborted);

        if (snapshot is null)
        {
            await EndSessionAsync(context, SessionEndReason.Revoked);
            return;
        }

        // Deactivation does not rotate the security stamp and an access window lapses on its own, so
        // both are checked here, per request, rather than only at sign-in.
        if (!snapshot.IsUsable(now))
        {
            await EndSessionAsync(context,
                snapshot.IsActive ? SessionEndReason.AccessExpired : SessionEndReason.Deactivated);
            return;
        }

        var identity = new ClaimsIdentity(AuthSchemes.AppCookie, "name", "roles");
        foreach (var claim in context.Principal!.Claims.Where(c => c.Type is "sub" or "name" or "preferred_username"
                                                                     or AuthSchemes.SessionIdClaim))
            identity.AddClaim(claim);
        identity.AddClaims(snapshot.Claims);
        if (snapshot.MustChangePassword)
            identity.AddClaim(new Claim(AuthSchemes.MustChangePasswordClaim, "true"));

        // No ShouldRenew: the enrichment is recomputed per request and must not be written into the ticket.
        context.ReplacePrincipal(new ClaimsPrincipal(identity));
    }

    private static async Task EndSessionAsync(CookieValidatePrincipalContext context, SessionEndReason reason)
    {
        context.RejectPrincipal();
        context.HttpContext.Items[AuthSchemes.EndReasonItem] = reason;
        context.HttpContext.Items[AuthSchemes.EndHintItem] = reason;
        await context.HttpContext.SignOutAsync(AuthSchemes.AppCookie);
    }

    /// <summary>
    /// API callers get a 401 (the SPA then navigates to /auth/login). Only a browser NAVIGATING to the
    /// server-rendered Hangfire dashboard — its page, or its iframe — is sent through sign-in. The dashboard's own
    /// background XHR polls are not: each such challenge would plant correlation and nonce cookies for an hour and
    /// send the IdP a prompt=login, so a forgotten tab would bury the browser in cookies (until a 400 "header too
    /// large" locks it out) and keep signing the user out of the IdP in other tabs.
    /// </summary>
    public static Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        var request = context.Request;
        if (request.Path.StartsWithSegments("/hangfire") && IsDocumentNavigation(request))
            return context.HttpContext.ChallengeAsync(AuthSchemes.Oidc,
                new AuthenticationProperties { RedirectUri = request.PathBase + request.Path + request.QueryString });

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        // Only when a session cookie was presented and its session is gone; the SPA tells "timed out" from "ended".
        // Nothing when the user ended it themselves (signed out in another tab, signed in again): telling them
        // their session "was ended" would be wrong.
        if (context.HttpContext.Items[AuthSchemes.EndHintItem] is SessionEndReason ended
            and not (SessionEndReason.Logout or SessionEndReason.Replaced))
            context.Response.Headers[SessionEndHeader] = ended == SessionEndReason.Idle ? "idle" : "ended";
        // Debug: a forgotten Hangfire tab polls every 2 s after its session ends, which at Information is tens of thousands of rows a day.
        Logger(context.HttpContext).LogDebug(
            "[AUTH-401] {Method} {Path} has no valid session", request.Method, request.Path.Value);
        return Task.CompletedTask;
    }

    /// <summary>
    /// A top-level or iframe page load, as opposed to a script's XHR/fetch. Browsers say so with
    /// <c>Sec-Fetch-Mode</c>/<c>Sec-Fetch-Dest</c>; without them (old browsers, proxies that strip them) fall back to
    /// "asks for HTML, and is not a jQuery XHR".
    /// </summary>
    internal static bool IsDocumentNavigation(HttpRequest request)
    {
        var mode = request.Headers["Sec-Fetch-Mode"].ToString();
        var dest = request.Headers["Sec-Fetch-Dest"].ToString();
        if (mode.Length > 0 || dest.Length > 0)
            return mode == "navigate" || dest is "document" or "iframe";

        return HttpMethods.IsGet(request.Method)
               && request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase)
               && request.Headers["X-Requested-With"].ToString() != "XMLHttpRequest";
    }

    public static Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (context.Request.Path.StartsWithSegments("/hangfire"))
            context.Response.Redirect(context.RedirectUri);
        else
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }

    // ── OIDC handler ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the app principal from the id_token: identity only, never permissions (those are added per
    /// request). Refuses accounts that cannot be used. The id_token is kept for the IdP end-session call.
    /// </summary>
    public static async Task TokenValidatedAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        var sub = principal?.FindFirstValue("sub");
        var name = principal?.FindFirstValue("name");
        if (!Guid.TryParse(sub, out var userId) || string.IsNullOrEmpty(name))
        {
            context.Fail("The id_token carries no usable subject.");
            return;
        }

        var services = context.HttpContext.RequestServices;
        var builder = services.GetRequiredService<AuthorizationClaimsBuilder>();
        // Reload replaces any snapshot cached before the change that prompted this sign-in, and caches the fresh
        // one so the first request does not read it again.
        var snapshot = await builder.ReloadAsync(userId, context.HttpContext.RequestAborted);
        if (snapshot is null || !snapshot.IsUsable(services.GetRequiredService<IDateTimeProvider>().ApplicationNow))
        {
            context.Fail("The account cannot be used.");
            return;
        }

        var identity = new ClaimsIdentity(AuthSchemes.AppCookie, "name", "roles");
        identity.AddClaim(new Claim("sub", sub));
        identity.AddClaim(new Claim("name", name));
        if (principal!.FindFirstValue("preferred_username") is { } preferredUsername)
            identity.AddClaim(new Claim("preferred_username", preferredUsername));
        context.Principal = new ClaimsPrincipal(identity);

        var idToken = context.TokenEndpointResponse?.IdToken ?? context.ProtocolMessage.IdToken;
        if (!string.IsNullOrEmpty(idToken))
            context.Properties!.StoreTokens([new AuthenticationToken { Name = "id_token", Value = idToken }]);
    }

    /// <summary>
    /// Sends the browser to the identity provider's public address. Authority is the internal base URL (the load
    /// balancer's certificate does not match the node's own name), so the discovery document names internal
    /// endpoints. The redirect URI is the API's own public address, pinned because only the scheme is forwarded.
    /// </summary>
    public static Task RedirectToIdentityProvider(RedirectContext context)
    {
        var urls = context.HttpContext.RequestServices.GetRequiredService<BffUrls>();
        var message = context.ProtocolMessage;
        message.IssuerAddress = urls.ToPublic(message.IssuerAddress);
        message.RedirectUri = urls.PublicBase + context.Options.CallbackPath;
        // Set by /auth/login ("Continue as ..."); the handler itself does not copy it onto the authorize request.
        message.LoginHint = context.Properties.GetParameter<string>(OpenIdConnectParameterNames.LoginHint);
        return Task.CompletedTask;
    }

    /// <summary>
    /// The handler does not send <c>client_id</c> on sign-out, and a 302 cannot be followed by the SPA's
    /// XHR, so the URL is handed back to <c>POST /auth/logout</c> to return as JSON.
    /// </summary>
    public static Task RedirectToIdentityProviderForSignOut(RedirectContext context)
    {
        var urls = context.HttpContext.RequestServices.GetRequiredService<BffUrls>();
        var message = context.ProtocolMessage;
        message.IssuerAddress = urls.ToPublic(message.IssuerAddress);
        message.ClientId = context.Options.ClientId;
        message.PostLogoutRedirectUri = urls.PublicBase + context.Options.SignedOutCallbackPath;
        // The handler adds `state` only after this event returns, and we end the response here, so do it
        // ourselves: it is how /signout-callback-oidc knows where to send the browser (the SPA).
        message.State = context.Options.StateDataFormat.Protect(context.Properties);

        context.HttpContext.Items[AuthSchemes.LogoutUrlItem] = message.CreateLogoutRequestUrl();
        context.HandleResponse();
        return Task.CompletedTask;
    }

    /// <summary>
    /// One live session per browser: when a new sign-in succeeds it replaces whatever session this browser still
    /// holds, so a stale bookmark or a second user on the same machine never lands in — or keeps — the previous
    /// user's session. Done here, on completion, rather than when the sign-in starts: starting one is a plain
    /// GET and must not change state.
    /// </summary>
    public static async Task TicketReceivedAsync(TicketReceivedContext context)
    {
        var http = context.HttpContext;

        // "Continue as A" that was signed in as B must not land on A's page: keep only the origin of the return URL
        // (already checked against the allowed SPA origins), or the SPA home when it is a path on this API.
        if (context.Properties?.Items.Remove(AuthSchemes.LoginHintItem, out var hint) == true
            && !string.Equals(hint, context.Principal?.FindFirstValue("name"), StringComparison.OrdinalIgnoreCase))
            context.ReturnUri = Uri.TryCreate(context.ReturnUri, UriKind.Absolute, out var target)
                ? target.GetLeftPart(UriPartial.Authority) + "/"
                : http.RequestServices.GetRequiredService<BffUrls>().SpaHome;

        // The sign-in completed: forget that an earlier attempt failed.
        http.Response.Cookies.Delete(RetryCookie(http), RetryCookieOptions());

        var options = http.RequestServices.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(AuthSchemes.AppCookie);
        var cookieName = options.Cookie.Name!;
        var cookie = options.CookieManager.GetRequestCookie(http, cookieName);
        if (string.IsNullOrEmpty(cookie)) return;

        // End the old session by its id...
        if (options.TicketDataFormat.Unprotect(cookie)?.Principal.FindFirstValue(CookieSessionIdClaim) is { } oldKey)
        {
            http.Items[AuthSchemes.EndReasonItem] = SessionEndReason.Replaced;
            await options.SessionStore!.RemoveAsync(oldKey, http, http.RequestAborted);
        }

        // ...and make the cookie handler forget the cookie for the rest of this request. Signing in over a live
        // cookie makes it RENEW that session's row with the new ticket instead of starting one — the new user would
        // inherit the previous user's session id (a cookie copied before the switch would then open the new
        // user's session), and the row would keep the previous user's id.
        http.Request.Headers.Cookie = WithoutCookie(http.Request.Headers.Cookie, cookieName);
    }

    /// <summary>
    /// The Cookie header without the named cookie (and its chunks, <c>{name}C1</c>, <c>{name}C2</c>…). Works on
    /// the raw segments, so every other cookie keeps its exact encoded value; rebuilding from the decoded
    /// <c>Request.Cookies</c> would change them.
    /// </summary>
    internal static StringValues WithoutCookie(StringValues header, string name)
    {
        var kept = header
            .SelectMany(line => (line ?? "").Split(';'))
            .Select(segment => segment.Trim())
            .Where(segment => segment.Length > 0)
            .Where(segment =>
            {
                var cookieName = segment[..(segment.IndexOf('=') is var eq and >= 0 ? eq : segment.Length)].Trim();
                return !(cookieName == name
                         || (cookieName.StartsWith(name, StringComparison.Ordinal)
                             && cookieName.Length > name.Length + 1
                             && cookieName[name.Length] == 'C'
                             && cookieName[(name.Length + 1)..].All(char.IsAsciiDigit)));
            })
            .ToArray();
        return kept.Length == 0 ? StringValues.Empty : string.Join("; ", kept);
    }

    // The claim the cookie handler stores the session key in (CookieAuthenticationHandler.SessionIdClaim, internal).
    private const string CookieSessionIdClaim = "Microsoft.AspNetCore.Authentication.Cookies-SessionId";

    // Remembers, for a few minutes and only on the callback path, that this browser just failed a sign-in and was
    // sent round again. Needed when the failing callback's state cannot be read (the usual stale-bookmark case),
    // because the state is where the "second attempt" flag normally rides.
    private static string RetryCookie(HttpContext http) =>
        http.RequestServices.GetRequiredService<IOptions<BrowserSessionOptions>>().Value.RetryCookieName;

    private static CookieOptions RetryCookieOptions() => new()
    {
        Path = "/signin-oidc",
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        MaxAge = TimeSpan.FromMinutes(5)
    };

    /// <summary>
    /// A callback whose state/correlation no longer match — typically a stale bookmark — starts sign-in again, and a
    /// second failure in a row stops instead of looping. Any other failure stops at once. It deliberately ends
    /// nothing: this request is a GET anyone can aim at a victim, and the previous session is replaced by the
    /// sign-in that follows.
    /// </summary>
    public static async Task RemoteFailureAsync(RemoteFailureContext context)
    {
        context.HandleResponse();
        var http = context.HttpContext;
        var reason = context.Failure?.Message ?? "unknown";
        var logger = Logger(http);
        logger.LogWarning("[AUTH-OIDC-FAILURE] {Reason}", reason);

        // A callback whose state or correlation cookie cannot be matched is what a host mismatch looks like: the
        // sign-in was started under one host and came back under AppBaseUrl's, where the cookies it set are not
        // sent. A stale bookmark fails the same way, so this only points at the usual cause.
        var urls = http.RequestServices.GetRequiredService<BffUrls>();
        var hint = $"If this keeps happening, check that VITE_API_URL equals AppBaseUrl (this request was for host "
                   + $"{http.Request.Host.Value}; AppBaseUrl is {urls.PublicBase}).";
        var stateFailure = context.Properties is null
                           || reason.Contains("Correlation failed", StringComparison.OrdinalIgnoreCase);
        if (stateFailure)
            logger.LogError(
                "[AUTH-OIDC-STATE-FAILURE] Sign-in callback could not be matched to its start (request host {RequestHost}, "
                + "AppBaseUrl {AppBaseUrl}). Check that VITE_API_URL equals AppBaseUrl.",
                http.Request.Host.Value, urls.PublicBase);

        // The attempt that failed was already a retry if its state says so; when the state cannot be read there is
        // no state to ask, so the cookie left by the previous failure answers instead.
        var alreadyRetried = context.Properties is { } properties
            ? properties.Items.ContainsKey(AuthSchemes.RetryItem)
            : http.Request.Cookies.ContainsKey(RetryCookie(http));
        // Only a state/correlation failure is worth another attempt (the stale-bookmark case). The rest — a back-channel
        // error, an account rejected in TokenValidated — would repeat, and a retry only makes the user type the password twice.
        if (!stateFailure || alreadyRetried)
        {
            http.Response.Cookies.Delete(RetryCookie(http), RetryCookieOptions());
            await Results.Problem(title: "Sign-in failed", detail: "Sign-in could not be completed. " + hint,
                    statusCode: StatusCodes.Status401Unauthorized)
                .ExecuteAsync(http);
            return;
        }

        http.Response.Cookies.Append(RetryCookie(http), "1", RetryCookieOptions());
        var returnUrl = urls.ResolveReturnUrl(context.Properties?.RedirectUri);
        http.Response.Redirect($"{urls.PublicBase}/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}&retry=1");
    }

    internal static ILogger Logger(HttpContext http) =>
        http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(LoggerCategory);
}
