using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.SetFinalValue;

/// <summary>
/// Endpoint: POST /pricing-analysis/{id}/methods/{methodId}/final-value
/// Sets final value for a pricing method
/// </summary>
public class SetFinalValueEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/pricing-analysis/{id:guid}/methods/{methodId:guid}/final-value",
                async (
                    Guid id,
                    Guid methodId,
                    SetFinalValueRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new SetFinalValueCommand(
                        id,
                        methodId,
                        request.FinalValue,
                        request.FinalValueRounded,
                        request.IncludeLandArea,
                        request.LandArea,
                        request.AppraisalPrice,
                        request.AppraisalPriceRounded,
                        request.HasBuildingCost,
                        request.BuildingCost,
                        request.AppraisalPriceWithBuilding,
                        request.AppraisalPriceWithBuildingRounded
                    );

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<SetFinalValueResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("SetFinalValue")
            .Produces<SetFinalValueResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Set final value for pricing method")
            .WithDescription("Creates or updates the final value for a specific pricing method. Supports optional land area and building cost calculations.")
            .WithTags("PricingAnalysis");
    }
}
