using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Auth.Application.Features.Clients.GetClient;

public class GetClientEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/auth/clients/{id}", async (
                string id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetClientQuery(id), cancellationToken);
                return Results.Ok(result);
            })
            .WithName("GetClient")
            .WithTags("Admin - OAuth Clients")
            .Produces<ClientDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization("OAuthClientsManage");
    }
}
