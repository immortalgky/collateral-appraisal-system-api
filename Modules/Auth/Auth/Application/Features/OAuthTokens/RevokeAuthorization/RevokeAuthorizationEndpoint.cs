using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Auth.Application.Features.OAuthTokens.RevokeAuthorization;

public class RevokeAuthorizationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/authorizations/{id}/revoke", async (
                string id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                await sender.Send(new RevokeAuthorizationCommand(id), cancellationToken);
                return Results.NoContent();
            })
            .WithName("RevokeAuthorization")
            .WithTags("Admin - OAuth Tokens")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization("OAuthTokensRevoke");
    }
}
