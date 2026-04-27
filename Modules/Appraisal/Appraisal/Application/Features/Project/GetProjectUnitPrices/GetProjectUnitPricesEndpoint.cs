namespace Appraisal.Application.Features.Project.GetProjectUnitPrices;

public class GetProjectUnitPricesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/project/unit-prices",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetProjectUnitPricesQuery(appraisalId);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result.UnitPrices);
                }
            )
            .WithName("GetProjectUnitPrices")
            .Produces<List<ProjectUnitPriceDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get project unit prices")
            .WithDescription("Retrieves unit prices for all units in a project. Returns every unit including those without calculated prices (LEFT JOIN). Works for both Condo and LandAndBuilding project types.")
            .WithTags("Project");
    }
}
