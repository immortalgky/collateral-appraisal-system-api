using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.AddApproach;

public class AddApproachEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/pricing-analysis/{id}/approaches",
                async (
                    Guid id,
                    AddApproachRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new AddApproachCommand(
                        id,
                        request.ApproachType,
                        request.Weight);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<AddApproachResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("AddApproach")
            .Produces<AddApproachResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Add approach to pricing analysis")
            .WithDescription("Adds a new approach (Market, Cost, or Income) to a pricing analysis.")
            .WithTags("PricingAnalysis");
    }
}
