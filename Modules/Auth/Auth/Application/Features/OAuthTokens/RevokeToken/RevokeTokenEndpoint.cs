using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Auth.Application.Features.OAuthTokens.RevokeToken;

public class RevokeTokenEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/tokens/{id}/revoke", async (
                string id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                await sender.Send(new RevokeTokenCommand(id), cancellationToken);
                return Results.NoContent();
            })
            .WithName("RevokeToken")
            .WithTags("Admin - OAuth Tokens")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization("OAuthTokensRevoke");
    }
}
