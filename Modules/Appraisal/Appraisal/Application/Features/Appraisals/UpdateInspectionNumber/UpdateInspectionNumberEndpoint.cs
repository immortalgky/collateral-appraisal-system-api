namespace Appraisal.Application.Features.Appraisals.UpdateInspectionNumber;

public class UpdateInspectionNumberEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/inspection-number",
                async (
                    Guid appraisalId,
                    UpdateInspectionNumberRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    await sender.Send(
                        new UpdateInspectionNumberCommand(appraisalId, request.InspectionNumber),
                        cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("UpdateInspectionNumber")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Update construction-inspection round number")
            .WithDescription("Corrects the round number of a Progressive appraisal. Affects only this appraisal.")
            .AddEndpointFilter<RejectClosedAppraisalWriteFilter>()
            .WithTags("Appraisal")
            .RequireAuthorization();
    }
}
