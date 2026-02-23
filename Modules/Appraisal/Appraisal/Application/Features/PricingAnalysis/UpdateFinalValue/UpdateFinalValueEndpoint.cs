using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.UpdateFinalValue;

/// <summary>
/// Endpoint: PUT /pricing-analysis/{id}/final-values/{valueId}
/// Updates an existing final value
/// </summary>
public class UpdateFinalValueEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/pricing-analysis/{id:guid}/final-values/{valueId:guid}",
                async (
                    Guid id,
                    Guid valueId,
                    UpdateFinalValueRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new UpdateFinalValueCommand(
                        id,
                        valueId,
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

                    var response = result.Adapt<UpdateFinalValueResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("UpdateFinalValue")
            .Produces<UpdateFinalValueResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update final value")
            .WithDescription("Updates an existing final value including optional land area and building cost calculations.")
            .WithTags("PricingAnalysis");
    }
}
