using Auth.Application.Helpers;

namespace Auth.Domain.Auth.Features.RefreshToken;

public record RefreshTokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    string Scope,
    string IdToken);

public class RefreshTokenEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/refresh", async (ISender sender, HttpContext httpContext) =>
        {
            var refreshToken = RefreshTokenCookieHelper.GetRefreshTokenFromCookie(httpContext);

            if (string.IsNullOrEmpty(refreshToken))
                return Results.Unauthorized();

            RefreshTokenResult result;
            try
            {
                result = await sender.Send(new RefreshTokenCommand(refreshToken));
            }
            catch (InvalidRefreshTokenException)
            {
                // Expected every time a session ends — idle timeout, revocation, or a rotation race
                // between tabs. Answer exactly like the missing-cookie branch above instead of letting
                // it bubble to the global handler, which would log an error and return 500 for routine
                // behaviour. Drop the dead cookie too, so there is nothing left for a browser to hand
                // back on the next request (or to revive through session restore).
                RefreshTokenCookieHelper.ClearRefreshTokenCookie(httpContext);
                return Results.Unauthorized();
            }

            // Set new refresh token cookie (token rotation)
            if (!string.IsNullOrEmpty(result.RefreshToken))
                RefreshTokenCookieHelper.SetRefreshTokenCookie(httpContext, result.RefreshToken);

            var response = new RefreshTokenResponse(
                result.AccessToken,
                result.TokenType,
                result.ExpiresIn,
                result.Scope,
                result.IdToken);

            return Results.Ok(response);
        })
        .AllowAnonymous();
    }
}
