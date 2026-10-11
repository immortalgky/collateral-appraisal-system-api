using Auth.Domain.Sessions;
using Auth.Infrastructure.Session;
using Microsoft.AspNetCore.Authentication;

namespace Auth.Application.Features.Auth.Logout;

public class LogoutEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Anonymous on purpose: logging out must work for a session that has just expired, and it only
        // ever ends the caller's own cookie session. The CSRF middleware guards the cookie case.
        app.MapPost("/auth/logout", async (HttpContext http, BffUrls urls, ILogger<LogoutEndpoint> logger) =>
            {
                // Build the IdP end-session URL first: it reads the id_token out of the live session.
                // The handler hands the URL back through HttpContext.Items instead of redirecting, because
                // an XHR cannot follow a 302 as a navigation.
                string? url = null;
                try
                {
                    await http.SignOutAsync(AuthSchemes.Oidc, new AuthenticationProperties { RedirectUri = urls.SpaHome });
                    url = http.Items[AuthSchemes.LogoutUrlItem] as string;
                }
                catch (Exception ex)
                {
                    // IdP discovery unreachable: the local session must still end.
                    logger.LogWarning(ex, "Could not build the IdP end-session URL; ending the local session only");
                }

                http.Items[AuthSchemes.EndReasonItem] = SessionEndReason.Logout;
                await http.SignOutAsync(AuthSchemes.AppCookie);

                return Results.Ok(new LogoutResponse(url ?? urls.SpaHome));
            })
            .AllowAnonymous()
            .AllowWhilePasswordChangeRequired()
            .WithName("Logout")
            .Produces<LogoutResponse>()
            .WithSummary("End the browser session")
            .WithDescription(
                "Ends the app session and returns the IdP end-session URL; the SPA then navigates to it so the "
                + "IdP clears its own cookie and returns to the SPA home.")
            .WithTags("Auth");
    }
}

public record LogoutResponse(string Url);
