namespace Appraisal.Application.Features.Project.CalculateProjectUnitPrices;

public class CalculateProjectUnitPricesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/project/unit-prices/calculate",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new CalculateProjectUnitPricesCommand(appraisalId);
                    await sender.Send(command, cancellationToken);
                    return Results.NoContent();
                }
            )
            .WithName("CalculateProjectUnitPrices")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Calculate project unit prices")
            .WithDescription("Runs the unit-price calculation for all units in a project. Works for both Condo and LandAndBuilding project types.")
            .WithTags("Project");
    }
}
