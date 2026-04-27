namespace Appraisal.Application.Features.Project.SaveProject;

public class SaveProjectEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/project",
                async (
                    Guid appraisalId,
                    SaveProjectRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<SaveProjectCommand>()
                        with { AppraisalId = appraisalId };

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<SaveProjectResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("SaveProject")
            .Produces<SaveProjectResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Save project")
            .WithDescription("Creates or updates the project for an appraisal. ProjectType discriminates Condo vs LandAndBuilding.")
            .WithTags("Project");
    }
}
