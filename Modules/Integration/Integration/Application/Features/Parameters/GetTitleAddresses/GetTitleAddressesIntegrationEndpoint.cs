using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Parameter.Addresses.Features.GetTitleAddresses;
using Parameter.Contracts.Parameters.Dtos;

namespace Integration.Application.Features.Parameters.GetTitleAddresses;

// Mirrors legacy GET /parameters/addresses/title for LOS (Parameter/Addresses/Features/GetTitleAddresses/GetTitleAddressesEndpoint.cs).
public class GetTitleAddressesIntegrationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/parameters/addresses/title", async (ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetTitleAddressesQuery(), cancellationToken);

                return Results.Ok(result.Addresses);
            })
            .WithName("Integration_GetTitleAddresses")
            .WithTags("Integration - Parameters")
            .Produces<List<AddressDto>>(StatusCodes.Status200OK)
            .RequireAuthorization("Integration");
    }
}
