namespace Appraisal.Application.Features.Assignments.AssignAppraisal;

public class AssignAppraisalEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/assignments",
                async (
                    Guid appraisalId,
                    AssignAppraisalRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new AssignAppraisalCommand(
                        appraisalId,
                        request.AssignmentType,
                        request.AssigneeUserId,
                        request.AssigneeCompanyId,
                        request.AssignmentMethod ?? "Manual",
                        request.InternalAppraiserId,
                        request.AssignedBy ?? string.Empty);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<AssignAppraisalResponse>();
                    return Results.Ok(response);
                }
            )
            .WithName("AssignAppraisal")
            .Produces<AssignAppraisalResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Assign appraisal")
            .WithDescription("Assign an appraisal to an internal user or external company.")
            .WithTags("Assignment");
    }
}
