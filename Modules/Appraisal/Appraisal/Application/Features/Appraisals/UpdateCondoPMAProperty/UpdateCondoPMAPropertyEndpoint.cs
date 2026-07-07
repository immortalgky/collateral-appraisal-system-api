namespace Appraisal.Application.Features.Appraisals.UpdateCondoPMAProperty;

public class UpdateCondoPMAPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/condo-pma",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    UpdateCondoPMAPropertyRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<UpdateCondoPMAPropertyCommand>()
                        with
                        {
                            AppraisalId = appraisalId, PropertyId = propertyId
                        };

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("UpdateCondoPMAProperty")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update condo pma property detail")
            .WithDescription("Update the detail of a condo pma property.")
            .WithTags("Appraisal Properties");
    }
}