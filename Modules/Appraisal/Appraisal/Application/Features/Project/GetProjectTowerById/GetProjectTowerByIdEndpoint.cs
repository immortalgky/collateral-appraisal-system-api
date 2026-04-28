using Appraisal.Application.Features.Project.GetProjectTowers;

namespace Appraisal.Application.Features.Project.GetProjectTowerById;

public class GetProjectTowerByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/project/towers/{towerId:guid}",
                async (
                    Guid appraisalId,
                    Guid towerId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetProjectTowerByIdQuery(appraisalId, towerId);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result.Tower);
                }
            )
            .WithName("GetProjectTowerById")
            .Produces<ProjectTowerDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get project tower by ID")
            .WithDescription("Retrieves a single project tower by its ID.")
            .WithTags("Project");
    }
}
