using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Parameter.Addresses.Features.GetDopaAddresses;
using Parameter.Contracts.Parameters.Dtos;

namespace Integration.Application.Features.Parameters.GetDopaAddresses;

// Mirrors legacy GET /parameters/addresses/dopa for LOS (Parameter/Addresses/Features/GetDopaAddresses/GetDopaAddressesEndpoint.cs).
public class GetDopaAddressesIntegrationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/parameters/addresses/dopa", async (ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetDopaAddressesQuery(), cancellationToken);

                return Results.Ok(result.Addresses);
            })
            .WithName("Integration_GetDopaAddresses")
            .WithTags("Integration - Parameters")
            .Produces<List<AddressDto>>(StatusCodes.Status200OK)
            .RequireAuthorization("Integration");
    }
}
