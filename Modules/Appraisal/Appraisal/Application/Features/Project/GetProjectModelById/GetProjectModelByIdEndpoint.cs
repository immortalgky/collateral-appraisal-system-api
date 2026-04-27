using Appraisal.Application.Features.Project.GetProjectModels;

namespace Appraisal.Application.Features.Project.GetProjectModelById;

public class GetProjectModelByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/project/models/{modelId:guid}",
                async (
                    Guid appraisalId,
                    Guid modelId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetProjectModelByIdQuery(appraisalId, modelId);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result.Model);
                }
            )
            .WithName("GetProjectModelById")
            .Produces<ProjectModelDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get project model by ID")
            .WithDescription("Retrieves a single project model by its ID.")
            .WithTags("Project");
    }
}
