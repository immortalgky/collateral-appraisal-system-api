namespace Appraisal.Application.Features.Assignments.GetAssignmentDefaults;

public class GetAssignmentDefaultsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/assignment-defaults",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetAssignmentDefaultsQuery(appraisalId);
                    var result = await sender.Send(query, cancellationToken);

                    return Results.Ok(result);
                }
            )
            .WithName("GetAssignmentDefaults")
            .Produces<GetAssignmentDefaultsResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get assignment defaults")
            .WithDescription(
                "Get the pre-selected assignment type, assignment method and internal follow-up method " +
                "for an appraisal, based on its appraisal type.")
            .WithTags("Assignment");
    }
}
