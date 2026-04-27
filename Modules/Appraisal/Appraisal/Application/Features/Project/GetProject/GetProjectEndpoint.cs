namespace Appraisal.Application.Features.Project.GetProject;

public class GetProjectEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/project",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetProjectQuery(appraisalId);

                    var result = await sender.Send(query, cancellationToken);

                    if (result is null)
                        return Results.NoContent();

                    var response = result.Adapt<GetProjectResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetProject")
            .Produces<GetProjectResponse>()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get project")
            .WithDescription("Retrieves the project for an appraisal. Returns 204 if no project has been created yet.")
            .WithTags("Project");
    }
}
