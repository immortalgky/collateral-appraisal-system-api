namespace Appraisal.Application.Features.Appraisals.CorrectAppraisalDocuments;

public class CorrectAppraisalDocumentsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/document-corrections",
                async (
                    Guid appraisalId,
                    CorrectAppraisalDocumentsRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new CorrectAppraisalDocumentsCommand(
                        appraisalId,
                        request.Reason,
                        request.RemoveId,
                        request.Add);

                    var result = await sender.Send(command, cancellationToken);

                    return Results.Ok(result);
                }
            )
            .WithName("CorrectAppraisalDocuments")
            .Produces<CorrectAppraisalDocumentsResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Attach, delete or replace valuation documents on a Completed appraisal")
            .WithDescription(
                "Admin-only correction of the valuation document checklist on a Completed appraisal. " +
                "Add only attaches, removeId only deletes, both replaces. Requires a reason and records " +
                "an audit entry in the same transaction. Note this endpoint deliberately does NOT carry " +
                "RejectClosedAppraisalWriteFilter — it is the sanctioned way in.")
            .WithTags("Appraisal Data Correction")
            .RequireAuthorization("appraisal.data-correction");
    }
}
