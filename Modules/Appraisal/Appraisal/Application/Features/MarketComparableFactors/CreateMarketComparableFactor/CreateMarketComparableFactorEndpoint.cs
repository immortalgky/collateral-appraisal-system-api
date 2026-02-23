using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Appraisal.Application.Features.MarketComparableFactors.CreateMarketComparableFactor;

/// <summary>
/// Endpoint: POST /market-comparable-factors
/// Creates a new market comparable factor
/// </summary>
public class CreateMarketComparableFactorEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/market-comparable-factors", async (
                CreateMarketComparableFactorRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new CreateMarketComparableFactorCommand(
                    request.FactorCode,
                    request.FactorName,
                    request.FieldName,
                    request.DataType,
                    request.FieldLength,
                    request.FieldDecimal,
                    request.ParameterGroup);

                var result = await sender.Send(command, cancellationToken);

                return Results.Created(
                    $"/market-comparable-factors/{result.Id}",
                    new CreateMarketComparableFactorResponse(result.Id));
            })
            .WithName("CreateMarketComparableFactor")
            .WithSummary("Create a market comparable factor")
            .WithDescription("Creates a new market comparable factor with field metadata. Valid DataType values: Text, Numeric, Dropdown, Checkbox, Date, Radio. Dropdown and Radio types require ParameterGroup.")
            .Produces<CreateMarketComparableFactorResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("MarketComparableFactors");
    }
}
