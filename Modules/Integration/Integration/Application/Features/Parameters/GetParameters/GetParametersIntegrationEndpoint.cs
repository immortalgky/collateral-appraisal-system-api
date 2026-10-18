using Carter;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Parameter.Contracts.Parameters.Dtos;
using Parameter.Parameters.Features.GetParameter;

namespace Integration.Application.Features.Parameters.GetParameters;

// Mirrors legacy GET /parameters for LOS (Parameter/Parameters/Features/GetParameter/GetParameterEndpoint.cs).
public class GetParametersIntegrationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/parameters", async ([AsParameters] ParameterDto parameter, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetParametersQuery(parameter), cancellationToken);

                var response = result.Adapt<GetParametersResponse>();

                return Results.Ok(response.Parameters);
            })
            .WithName("Integration_GetParameters")
            .WithTags("Integration - Parameters")
            .Produces<List<ParameterDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireAuthorization("Integration");
    }
}
