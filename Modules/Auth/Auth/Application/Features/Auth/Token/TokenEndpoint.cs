using Auth.Application.Helpers;
using Microsoft.AspNetCore.Authorization;

namespace Auth.Domain.Auth.Features.Token;

public record TokenRequest(string GrantType, string ClientId, string Code, string CodeVerifier, string RedirectUri);

public record TokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    string Scope,
    string IdToken,
    string RefreshToken);

public record SecureTokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    string Scope,
    string IdToken);

public class TokenEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/token", async (TokenRequest request, ISender sender, HttpContext httpContext) =>
        {
            var command = request.Adapt<TokenCommand>();
            var result = await sender.Send(command);

            // Set refresh token as httpOnly cookie and strip it from response
            if (!string.IsNullOrEmpty(result.RefreshToken))
                RefreshTokenCookieHelper.SetRefreshTokenCookie(httpContext, result.RefreshToken);

            var secureResponse = new SecureTokenResponse(
                result.AccessToken,
                result.TokenType,
                result.ExpiresIn,
                result.Scope,
                result.IdToken);

            return Results.Ok(secureResponse);
        })
        .AllowAnonymous();
    }
}
