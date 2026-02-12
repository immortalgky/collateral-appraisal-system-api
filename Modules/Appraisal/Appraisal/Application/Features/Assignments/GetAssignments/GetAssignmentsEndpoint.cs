namespace Appraisal.Application.Features.Assignments.GetAssignments;

public class GetAssignmentsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/assignments",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetAssignmentsQuery(appraisalId);

                    var result = await sender.Send(query, cancellationToken);

                    return Results.Ok(new GetAssignmentsResponse(result.Assignments));
                }
            )
            .WithName("GetAssignments")
            .Produces<GetAssignmentsResponse>(StatusCodes.Status200OK)
            .WithSummary("Get assignments")
            .WithDescription("Get all assignments for an appraisal.")
            .WithTags("Assignment");
    }
}
