namespace Appraisal.Application.Features.Assignments.CancelAssignment;

public class CancelAssignmentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch(
                "/appraisals/{appraisalId:guid}/assignments/{assignmentId:guid}/cancel",
                async (
                    Guid appraisalId,
                    Guid assignmentId,
                    CancelAssignmentRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new CancelAssignmentCommand(
                        appraisalId,
                        assignmentId,
                        request.Reason);

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("CancelAssignment")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Cancel assignment")
            .WithDescription("Cancel an appraisal assignment with a reason.")
            .WithTags("Assignment");
    }
}
