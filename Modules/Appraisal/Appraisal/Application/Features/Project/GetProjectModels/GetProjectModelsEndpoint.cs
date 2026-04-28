namespace Appraisal.Application.Features.Project.GetProjectModels;

public class GetProjectModelsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/project/models",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetProjectModelsQuery(appraisalId);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result.Models);
                }
            )
            .WithName("GetProjectModels")
            .Produces<List<ProjectModelDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get project models")
            .WithDescription("Retrieves all models for a project (Condo or LandAndBuilding).")
            .WithTags("Project");
    }
}
