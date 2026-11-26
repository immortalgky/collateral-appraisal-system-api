namespace Appraisal.Application.Features.Appraisals.SuspendAppraisal;

public class SuspendAppraisalEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch("/appraisals/{appraisalId:guid}/suspension", async (Guid appraisalId, SuspendAppraisalRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new SuspendAppraisalCommand(appraisalId, request.IsSuspended, request.Reason), cancellationToken);

            return Results.Ok(result);
        })
        .WithName("SuspendAppraisal")
        .Produces<SuspendAppraisalResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .WithSummary("Suspend or reactivate a Completed appraisal for external result retrieval")
        .WithTags("Appraisal Suspension")
        .RequireAuthorization("appraisal.suspension");
    }
}
