using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.UpdateVesselProperty;

public class UpdateVesselPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/vessel-detail",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    UpdateVesselPropertyRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<UpdateVesselPropertyCommand>()
                        with { AppraisalId = appraisalId, PropertyId = propertyId };

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("UpdateVesselProperty")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update vessel property detail")
            .WithDescription("Update the detail of a vessel property.")
            .WithTags("Appraisal Properties");
    }
}
