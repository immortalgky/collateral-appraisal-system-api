namespace Appraisal.Application.Features.Assignments.RejectAssignment;

public class RejectAssignmentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch(
                "/appraisals/{appraisalId:guid}/assignments/{assignmentId:guid}/reject",
                async (
                    Guid appraisalId,
                    Guid assignmentId,
                    RejectAssignmentRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new RejectAssignmentCommand(
                        appraisalId,
                        assignmentId,
                        request.Reason);

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("RejectAssignment")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Reject assignment")
            .WithDescription("Reject an appraisal assignment with a reason.")
            .WithTags("Assignment");
    }
}
