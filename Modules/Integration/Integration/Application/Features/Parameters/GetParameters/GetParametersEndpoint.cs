using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.Parameters.GetParameters;

public class GetParametersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/parameters", async (
            string? groups,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var groupList = string.IsNullOrWhiteSpace(groups)
                ? null
                : groups.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .ToList();

            var result = await sender.Send(new GetParametersQuery(groupList), cancellationToken);
            return Results.Ok(result);
        })
        .WithName("Integration_GetParameters")
        .WithTags("Integration - Parameters")
        .Produces<List<ParameterGroupResult>>()
        .RequireAuthorization("Integration");
    }
}
