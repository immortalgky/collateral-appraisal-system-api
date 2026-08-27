namespace Appraisal.Application.Features.Appraisals.CorrectPropertyData;

public class CorrectPropertyDataEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/data-correction",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    CorrectPropertyDataRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new CorrectPropertyDataCommand(
                        appraisalId,
                        propertyId,
                        request.Reason,
                        request.ToCorrectionData());

                    var result = await sender.Send(command, cancellationToken);

                    return Results.Ok(result);
                }
            )
            .WithName("CorrectPropertyData")
            .Produces<CorrectPropertyDataResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Correct descriptive property data on a Completed appraisal")
            .WithDescription(
                "Admin-only correction of descriptive/title/location/owner fields on a Completed " +
                "appraisal. Requires a reason and records a field-level audit entry. Fields that " +
                "feed pricing are not correctable here. Cancelled appraisals are out of scope and " +
                "stay read-only. Note this endpoint deliberately does NOT carry " +
                "RejectClosedAppraisalWriteFilter — it is the sanctioned way in.")
            .WithTags("Appraisal Data Correction")
            .RequireAuthorization("appraisal.data-correction");
    }
}
