using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.UpdateBuildingProperty;

public class UpdateBuildingPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/building-detail",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    UpdateBuildingPropertyRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<UpdateBuildingPropertyCommand>()
                        with { AppraisalId = appraisalId, PropertyId = propertyId };

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("UpdateBuildingProperty")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update building property detail")
            .WithDescription("Update the detail of a building property.")
            .WithTags("Appraisal Properties");
    }
}
