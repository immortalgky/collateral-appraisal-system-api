using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Appraisal.Application.Features.MarketComparableFactors.UpdateMarketComparableFactor;

/// <summary>
/// Endpoint: PUT /market-comparable-factors/{id}
/// Updates an existing market comparable factor
/// </summary>
public class UpdateMarketComparableFactorEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/market-comparable-factors/{id:guid}", async (
                Guid id,
                UpdateMarketComparableFactorRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new UpdateMarketComparableFactorCommand(
                    id,
                    request.FactorName,
                    request.FieldName,
                    request.FieldLength,
                    request.FieldDecimal,
                    request.ParameterGroup);

                var result = await sender.Send(command, cancellationToken);

                return Results.Ok(result);
            })
            .WithName("UpdateMarketComparableFactor")
            .WithSummary("Update a market comparable factor")
            .WithDescription("Updates an existing market comparable factor. Note: FactorCode and DataType are immutable and cannot be changed.")
            .Produces<UpdateMarketComparableFactorResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("MarketComparableFactors");
    }
}
