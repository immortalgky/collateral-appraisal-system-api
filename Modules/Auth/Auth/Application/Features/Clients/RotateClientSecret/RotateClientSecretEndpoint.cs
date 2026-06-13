using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Auth.Application.Features.Clients.RotateClientSecret;

public class RotateClientSecretEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/clients/{id}/secret", async (
                string id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new RotateClientSecretCommand(id), cancellationToken);
                return Results.Ok(result);
            })
            .WithName("RotateClientSecret")
            .WithTags("Admin - OAuth Clients")
            .Produces<RotateClientSecretResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization("OAuthClientsManage");
    }
}
