using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Auth.Application.Features.Clients.DeleteClient;

public class DeleteClientEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/auth/clients/{id}", async (
                string id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                await sender.Send(new DeleteClientCommand(id), cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeleteClient")
            .WithTags("Admin - OAuth Clients")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization("OAuthClientsManage");
    }
}
