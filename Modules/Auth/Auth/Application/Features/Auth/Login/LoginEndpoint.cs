using Auth.Infrastructure.Session;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Auth.Application.Features.Auth.Login;

public class LoginEndpoint : ICarterModule
{
    // Warn about a host mismatch once, not on every click of every confused user.
    private static int _hostMismatchLogged;

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // A top-level navigation from the SPA, not an XHR: it ends in a redirect chain through the IdP.
        app.MapGet("/auth/login", (HttpContext http, BffUrls urls, ILogger<LoginEndpoint> logger,
                IOptions<IdentityOptions> identity, string? returnUrl, string? retry,
                [FromQuery(Name = LoginHint.Parameter)] string? loginHint,
                [FromQuery(Name = LoginMethod.Parameter)] string? method) =>
            {
                // A request under another host than AppBaseUrl (the SPA's VITE_API_URL not equal to it) will sign in on
                // AppBaseUrl and then call an API whose cookie never arrives. Whether this is wrong depends on how the
                // load balancer presents the Host header, which the API cannot know, so it only warns (once) and goes
                // on; a sign-in that really cannot complete is diagnosed where it fails (RemoteFailureAsync).
                if (!urls.RequestMatchesPublicHost(http.Request)
                    && Interlocked.Exchange(ref _hostMismatchLogged, 1) == 0)
                    logger.LogWarning(
                        "[AUTH-HOST-MISMATCH] /auth/login was reached as host {RequestHost} but AppBaseUrl is {AppBaseUrl}. "
                        + "If sign-in fails or loops, check that VITE_API_URL equals AppBaseUrl.",
                        http.Request.Host.Value, urls.PublicBase);

                // A plain GET that only issues the challenge: it must not change state, or any same-site page
                // could sign users out with an <img>. The browser's previous session is replaced when the new
                // sign-in completes (AppSessionEvents.TicketReceivedAsync).
                var properties = new AuthenticationProperties { RedirectUri = urls.ResolveReturnUrl(returnUrl) };
                if (retry == "1") properties.Items[AuthSchemes.RetryItem] = "1";
                // The "Continue as ..." button: the IdP's sign-in page opens with this username filled in.
                // Not on the retry after a stale state: that one starts over without it.
                if (retry != "1"
                    && LoginHint.Sanitize(loginHint, identity.Value.User.AllowedUserNameCharacters) is { } hint)
                {
                    properties.SetParameter(OpenIdConnectParameterNames.LoginHint, hint);
                    properties.Items[AuthSchemes.LoginHintItem] = hint;
                }
                // "passkey" or "password": which sign-in page the IdP opens. Anything else is ignored (the IdP's default).
                if (LoginMethod.Sanitize(method) is { } signInMethod)
                    properties.SetParameter(LoginMethod.Parameter, signInMethod);
                return Results.Challenge(properties, [AuthSchemes.Oidc]);
            })
            .AllowAnonymous()
            .AllowWhilePasswordChangeRequired()
            .WithName("Login")
            .WithSummary("Start browser sign-in")
            .WithDescription(
                "Signs in through the OIDC flow and redirects to returnUrl (an absolute URL on an allowed SPA "
                + "origin; otherwise the SPA home). The browser's previous session ends when the new sign-in completes.")
            .WithTags("Auth");
    }
}
