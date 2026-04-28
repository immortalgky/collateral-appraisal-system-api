namespace Appraisal.Application.Features.Quotations.RemoveAppraisalFromDraft;

public class RemoveAppraisalFromDraftEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/quotations/{id:guid}/appraisals/{appraisalId:guid}",
                async (
                    Guid id,
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new RemoveAppraisalFromDraftCommand(id, appraisalId);
                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("RemoveAppraisalFromDraft")
            .Produces<RemoveAppraisalFromDraftResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Remove an appraisal from a Draft quotation")
            .WithDescription("Removes an appraisal from a Draft RFQ. If the last appraisal is removed, " +
                             "the Draft is automatically cancelled.")
            .WithTags("Quotation")
            .RequireAuthorization();
    }
}
