using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Auth.Application.Features.Scopes.DeleteScope;

public class DeleteScopeEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/auth/scopes/{id}", async (
                string id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                await sender.Send(new DeleteScopeCommand(id), cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeleteScope")
            .WithTags("Admin - OAuth Scopes")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization("OAuthScopesManage");
    }
}
