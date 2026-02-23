using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.UpdateApproach;

public class UpdateApproachEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/pricing-analysis/{id}/approaches/{approachId}",
                async (
                    Guid id,
                    Guid approachId,
                    UpdateApproachRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new UpdateApproachCommand(
                        id,
                        approachId,
                        request.ApproachValue,
                        request.Weight);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<UpdateApproachResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("UpdateApproach")
            .Produces<UpdateApproachResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update approach")
            .WithDescription("Updates the value and/or weight of an existing approach.")
            .WithTags("PricingAnalysis");
    }
}
