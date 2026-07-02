namespace Appraisal.Application.Features.Appraisals.UpdateLandPMAProperty;

public class UpdateLandPMAPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/land-building-pma",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    UpdateLandPMAPropertyRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<UpdateLandPMAPropertyCommand>()
                        with
                        {
                            AppraisalId = appraisalId, PropertyId = propertyId
                        };

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("UpdateLandPMAProperty")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update Land pma property detail")
            .WithDescription("Update the detail of a Land pma property.")
            .WithTags("Appraisal Properties");
    }
}