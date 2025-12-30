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

public class TokenEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/token", async (TokenRequest request, ISender sender) =>
        {
            var command = request.Adapt<TokenCommand>();
            var result = await sender.Send(command);
            return Results.Ok(result);
        })
        .AllowAnonymous();
    }
}