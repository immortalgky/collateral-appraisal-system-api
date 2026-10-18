using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Server.AspNetCore;
using Auth.Application.Services;
using Auth.Domain.Auditing;
using Auth.Domain.Identity;
using Auth.Infrastructure.Session;
using Microsoft.AspNetCore.Identity;
using Shared.Time;

namespace Auth.Application.Controllers;

[AllowWhilePasswordChangeRequired]
public class OpenIddictController(
    ITokenService tokenService,
    IAuthAuditWriter auditWriter,
    UserManager<ApplicationUser> userManager,
    IDateTimeProvider dateTimeProvider) : Controller
{
    [Authorize(AuthenticationSchemes = "Identity.Application")]
    [AllowAnonymous]
    [HttpGet("~/connect/authorize")]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest();
        if (request is null)
            return BadRequest(new { error = "Invalid request" });

        // prompt=login means the client wants a genuinely interactive sign-in. The API's OIDC handler
        // sends it on every challenge: it only challenges when the browser has no valid app session, so
        // the user must prove who they are again.
        //
        // Without this branch the Identity SSO cookie alone is enough to mint a fresh authorization
        // code, so a user who closed the browser (or idled out) would be signed straight back in
        // without typing a password, defeating the session-scoped app cookie. Relying on that
        // cookie expiring on its own is not sound: it is already a session cookie, yet Chromium
        // browsers configured to "continue where you left off" restore session cookies on relaunch.
        //
        // The prompt parameter is stripped from the return URL so the post-login redirect back here
        // does not sign the user out again in an endless loop.
        // Note this makes an anonymous GET destroy session state, so any site can top-level-navigate a
        // victim here and sign them out mid-work. Accepted rather than overlooked: prompt=login is a
        // GET by OIDC's own definition, and /connect/logout below is already an anonymous GET with
        // the same effect, so this widens no boundary that was not already open. The blast radius is
        // a forced re-login, never data access.
        if (request.HasPromptValue(OpenIddictConstants.PromptValues.Login))
        {
            await HttpContext.SignOutAsync("Identity.Application");
            return Redirect($"/Account/Login?ReturnUrl={Uri.EscapeDataString(BuildAuthorizeUrlWithoutPrompt())}");
        }

        if (HttpContext.User.Identity?.IsAuthenticated != true)
            // Not logged in → redirect to log in UI with returnUrl
            return Redirect(
                $"/Account/Login?ReturnUrl={Uri.EscapeDataString(HttpContext.Request.Path + HttpContext.Request.QueryString)}");

        // The Identity cookie outlives changes to the account behind it: deactivation does not rotate
        // the security stamp, and an access window can lapse while the browser sits on this tab. Minting
        // a fresh authorization code from a stale cookie would hand out working tokens either way, so
        // re-read the account and drop the session when it is no longer usable.
        var signedInUser = await userManager.GetUserAsync(HttpContext.User);
        if (signedInUser is null || !signedInUser.IsUsable(dateTimeProvider.ApplicationNow))
        {
            await HttpContext.SignOutAsync("Identity.Application");
            return Redirect(
                $"/Account/Login?ReturnUrl={Uri.EscapeDataString(BuildAuthorizeUrlWithoutPrompt())}");
        }

        var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        identity.AddClaim(OpenIddictConstants.Claims.Subject,
            HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        identity.AddClaim(OpenIddictConstants.Claims.Name, HttpContext.User.Identity?.Name ?? string.Empty);

        // Add destinations for claims
        foreach (var claim in identity.Claims)
            claim.SetDestinations(OpenIddictConstants.Destinations.AccessToken,
                OpenIddictConstants.Destinations.IdentityToken);

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request!.GetScopes());

        // Use SignIn method from Controller base class
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Rebuilds the current /connect/authorize URL with the prompt parameter removed, so it can be
    /// used as the post-login return URL without re-triggering the forced sign-out above. Every
    /// other parameter (client_id, state, PKCE challenge, …) is preserved verbatim.
    /// </summary>
    private string BuildAuthorizeUrlWithoutPrompt()
    {
        var query = QueryHelpers.ParseQuery(HttpContext.Request.QueryString.Value);
        query.Remove(OpenIddictConstants.Parameters.Prompt);

        var parameters = query.SelectMany(entry =>
            entry.Value.Select(value => KeyValuePair.Create(entry.Key, value)));

        return QueryHelpers.AddQueryString(HttpContext.Request.Path, parameters);
    }

    [AllowAnonymous]
    [HttpPost("~/connect/token")]
    public async Task<IActionResult> Token()
    {
        var request = HttpContext.GetOpenIddictServerRequest();

        if (request is null)
            return BadRequest(new { error = "Invalid request" });

        if (!request.IsAuthorizationCodeGrantType()
            && !request.IsClientCredentialsGrantType()
            && !request.IsRefreshTokenGrantType())
            return BadRequest(new { error = "Unsupported grant_type" });

        var principal = (await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme))
            .Principal;

        if (request.IsClientCredentialsGrantType())
            return await HandleClientCredentialsGrant(request);

        if (request.IsRefreshTokenGrantType())
            return await HandleRefreshTokenGrant(request, principal);

        return await HandleAuthorizationCodeGrant(request, principal);
    }

    private async Task<IActionResult> HandleAuthorizationCodeGrant(OpenIddictRequest request,
        ClaimsPrincipal? principal)
    {
        if (principal == null) return BadRequest(new { error = "Invalid authorization code" });

        var grant = await tokenService.CreateAuthCodeFlowAccessTokenPrincipal(request, principal);
        if (grant.Rejection is not null)
            return RejectGrant(grant.Rejection);

        return SignIn(grant.Principal!, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<IActionResult> HandleClientCredentialsGrant(OpenIddictRequest request)
    {
        var claimsPrincipal = await tokenService.CreateClientCredFlowAccessTokenPrincipal(request);
        return SignIn(claimsPrincipal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<IActionResult> HandleRefreshTokenGrant(OpenIddictRequest request, ClaimsPrincipal? principal)
    {
        if (principal == null) return BadRequest(new { error = "Invalid refresh token" });

        // Re-validate account state on every refresh so deactivation / forced password change /
        // password expiry take effect within one access-token lifetime instead of the whole
        // refresh-token lifetime. Rejecting forces the SPA back through interactive login. The
        // account is loaded once: the same call validates and builds the new principal.
        var refresh = await tokenService.CreateRefreshFlowPrincipalAsync(request, principal);
        if (refresh.Rejection is not null)
            return RejectGrant(refresh.Rejection);

        return SignIn(refresh.Principal!, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>Denies a grant with OpenIddict's standard invalid_grant response.</summary>
    private ForbidResult RejectGrant(string description) =>
        Forbid(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidGrant,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
            }));

    [AllowAnonymous]
    [HttpGet("~/connect/logout")]
    [HttpPost("~/connect/logout")]
    public async Task<IActionResult> Logout()
    {
        // /connect/logout is anonymous and may be hit without an active session, so only audit when
        // the SSO cookie names a user to attribute the logout to. The cookie has to be authenticated
        // explicitly: User comes from the default scheme (the app session), which is not this cookie.
        var idpUser = (await HttpContext.AuthenticateAsync("Identity.Application")).Principal;
        if (idpUser?.Identity?.IsAuthenticated == true)
        {
            Guid? userId = Guid.TryParse(idpUser.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
            await auditWriter.RecordAuthEventAsync(AuditAction.LoggedOut, userId, idpUser.Identity.Name);
        }

        await HttpContext.SignOutAsync("Identity.Application");

        return SignOut(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new AuthenticationProperties { RedirectUri = "/" });
    }
}