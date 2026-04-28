namespace Appraisal.Application.Features.Project.GetProjectPricingAssumptions;

public class GetProjectPricingAssumptionsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/project/pricing-assumptions",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetProjectPricingAssumptionsQuery(appraisalId);
                    var result = await sender.Send(query, cancellationToken);
                    return result.Assumption is null
                        ? Results.NoContent()
                        : Results.Ok(result.Assumption);
                }
            )
            .WithName("GetProjectPricingAssumptions")
            .Produces<ProjectPricingAssumptionDto>()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get project pricing assumptions")
            .WithDescription("Retrieves pricing assumptions for a project. Returns 204 if no assumptions have been saved yet. Works for both Condo and LandAndBuilding project types.")
            .WithTags("Project");
    }
}
