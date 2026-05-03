namespace Appraisal.Application.Features.Project.GetProjectModelPricingContext;

public class GetProjectModelPricingContextEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/projects/{projectId:guid}/models/{modelId:guid}/pricing-context",
                async (
                    Guid appraisalId,
                    Guid projectId,
                    Guid modelId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetProjectModelPricingContextQuery(appraisalId, projectId, modelId);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                }
            )
            .WithName("GetProjectModelPricingContext")
            .Produces<ProjectModelPricingContextDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get project model pricing context")
            .WithDescription(
                "Returns the flat pricing context (project + optional tower + model) for a specific model. " +
                "Tower is null for LandAndBuilding projects. " +
                "Used to auto-populate factor subject values on the pricing analysis page.")
            .WithTags("Project");
    }
}
