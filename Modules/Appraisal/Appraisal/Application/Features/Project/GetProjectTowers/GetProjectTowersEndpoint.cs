namespace Appraisal.Application.Features.Project.GetProjectTowers;

public class GetProjectTowersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/project/towers",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetProjectTowersQuery(appraisalId);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result.Towers);
                }
            )
            .WithName("GetProjectTowers")
            .Produces<List<ProjectTowerDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get project towers")
            .WithDescription("Retrieves all towers for a Condo project. Returns empty list for LandAndBuilding projects.")
            .WithTags("Project");
    }
}
