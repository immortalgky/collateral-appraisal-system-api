namespace Appraisal.Application.Features.Appraisals.UpdateRentalInfo;

public class UpdateRentalInfoEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/rental-info",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    UpdateRentalInfoRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<UpdateRentalInfoCommand>()
                        with { AppraisalId = appraisalId, PropertyId = propertyId };

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("UpdateRentalInfo")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update rental info")
            .WithDescription("Update the rental info for a lease agreement property.")
            .WithTags("Appraisal Properties");
    }
}
