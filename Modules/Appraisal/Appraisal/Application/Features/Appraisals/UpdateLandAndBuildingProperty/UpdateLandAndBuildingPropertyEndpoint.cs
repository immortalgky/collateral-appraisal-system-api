using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.UpdateLandAndBuildingProperty;

public class UpdateLandAndBuildingPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/land-and-building-detail",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    UpdateLandAndBuildingPropertyRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<UpdateLandAndBuildingPropertyCommand>()
                        with { AppraisalId = appraisalId, PropertyId = propertyId };

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("UpdateLandAndBuildingProperty")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update land and building property detail")
            .WithDescription("Update the detail of a land and building property.")
            .WithTags("Appraisal Properties");
    }
}
