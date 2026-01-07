using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.UpdateLandProperty;

public class UpdateLandPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/land-detail",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    UpdateLandPropertyRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<UpdateLandPropertyCommand>()
                        with { AppraisalId = appraisalId, PropertyId = propertyId };

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("UpdateLandProperty")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update land property detail")
            .WithDescription("Update the detail of a land property.")
            .WithTags("Appraisal Properties");
    }
}
