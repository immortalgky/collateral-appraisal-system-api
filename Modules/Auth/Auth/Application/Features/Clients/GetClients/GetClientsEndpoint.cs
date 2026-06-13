using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Pagination;

namespace Auth.Application.Features.Clients.GetClients;

public class GetClientsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/auth/clients", async (
                string? search,
                int? pageNumber,
                int? pageSize,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new GetClientsQuery(search, pageNumber ?? 1, pageSize ?? 20);
                var result = await sender.Send(query, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("GetClients")
            .WithTags("Admin - OAuth Clients")
            .Produces<PaginatedResult<ClientListItemDto>>(StatusCodes.Status200OK)
            .RequireAuthorization("OAuthClientsManage");
    }
}
