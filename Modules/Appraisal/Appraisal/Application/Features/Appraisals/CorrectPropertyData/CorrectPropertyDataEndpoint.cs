namespace Appraisal.Application.Features.Appraisals.CorrectPropertyData;

public class CorrectPropertyDataEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/data-correction/{suffix}",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    string suffix,
                    CorrectPropertyDataRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new CorrectPropertyDataCommand(
                        appraisalId,
                        propertyId,
                        suffix,
                        request.Reason,
                        request.Data);

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
            .WithSummary("Correct a property of a Completed appraisal through the real page's write logic")
            .WithDescription(
                "Admin-only correction of any property field on a Completed appraisal. {suffix} is the real " +
                "PUT route suffix for the property's type and data is exactly the body that PUT accepts. " +
                "Requires a reason and records a field-level audit entry in the same transaction. The " +
                "correction never recomputes valuation or touches pricing, so approved figures stay as " +
                "they were. Note this endpoint deliberately does NOT carry RejectClosedAppraisalWriteFilter " +
                "— it is the sanctioned way in.")
            .WithTags("Appraisal Data Correction")
            .RequireAuthorization("appraisal.data-correction");
    }
}
