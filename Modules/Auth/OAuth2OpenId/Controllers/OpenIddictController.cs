using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using OpenIddict.Server.AspNetCore;
using OAuth2OpenId.Services;

namespace OAuth2OpenId.Controllers;

public class OpenIddictController(ITokenService tokenService) : Controller
{
    [Authorize(AuthenticationSchemes = "Identity.Application")]
    [AllowAnonymous]
    [HttpGet("~/connect/authorize")]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest();

        if (HttpContext.User.Identity?.IsAuthenticated != true)
            // Not logged in → redirect to log in UI with returnUrl
            return Redirect(
                $"/Account/Login?ReturnUrl={Uri.EscapeDataString(HttpContext.Request.Path + HttpContext.Request.QueryString)}");

        // Auto-approve for demo (or show consent screen)
        var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        identity.AddClaim(OpenIddictConstants.Claims.Subject,
            HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        identity.AddClaim(OpenIddictConstants.Claims.Name, HttpContext.User.Identity.Name);

        // Add destinations for claims
        foreach (var claim in identity.Claims)
            claim.SetDestinations(OpenIddictConstants.Destinations.AccessToken,
                OpenIddictConstants.Destinations.IdentityToken);

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());

        // Use SignIn method from Controller base class
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [AllowAnonymous]
    [HttpPost("~/connect/token")]
    public async Task<IActionResult> Token()
    {
        var request = HttpContext.GetOpenIddictServerRequest();

        if (request is null)
            return BadRequest(new { error = "Invalid request" });

        if (!request.IsAuthorizationCodeGrantType() && !request.IsClientCredentialsGrantType())
        {
            return BadRequest(new { error = "Unsupported grant_type" });
        }

        var principal = (await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme))
            .Principal;

        if (request.IsClientCredentialsGrantType())
            return await HandleClientCredentialsGrant(request);

        return await HandleAuthorizationCodeGrant(request, principal);
    }

    private async Task<IActionResult> HandleAuthorizationCodeGrant(OpenIddictRequest request, ClaimsPrincipal principal)
    {
        if (principal == null) return BadRequest(new { error = "Invalid authorization code" });
        var claimsPrincipal = await tokenService.CreateAuthCodeFlowAccessTokenPrincipal(request, principal);
        return SignIn(claimsPrincipal, null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<IActionResult> HandleClientCredentialsGrant(OpenIddictRequest request)
    {
        var claimsPrincipal = await tokenService.CreateClientCredFlowAccessTokenPrincipal(request);
        return SignIn(claimsPrincipal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}