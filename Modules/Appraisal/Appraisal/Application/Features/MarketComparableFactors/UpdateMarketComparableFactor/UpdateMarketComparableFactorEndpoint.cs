using Carter;
using Mapster;
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
                    request.DataType,
                    request.FieldLength,
                    request.FieldDecimal,
                    request.ParameterGroup);

                var result = await sender.Send(command, cancellationToken);

                var response = result.Adapt<UpdateMarketComparableFactorResponse>();
                return Results.Ok(response);
            })
            .WithName("UpdateMarketComparableFactor")
            .WithSummary("Update a market comparable factor")
            .WithDescription("Updates an existing market comparable factor. Note: FactorCode is immutable and cannot be changed. Valid DataType values: Text, Numeric, Dropdown, Checkbox, Date, Radio. Dropdown and Radio types require ParameterGroup.")
            .Produces<UpdateMarketComparableFactorResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("MarketComparableFactors");
    }
}
