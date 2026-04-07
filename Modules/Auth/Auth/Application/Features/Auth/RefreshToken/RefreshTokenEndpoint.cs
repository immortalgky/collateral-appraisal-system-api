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

            var command = new RefreshTokenCommand(refreshToken);
            var result = await sender.Send(command);

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
