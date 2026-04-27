namespace Appraisal.Application.Features.Project.SaveProjectUnitPrices;

public class SaveProjectUnitPricesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/project/unit-prices",
                async (
                    Guid appraisalId,
                    SaveProjectUnitPricesRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new SaveProjectUnitPricesCommand(appraisalId, request.UnitPriceFlags);
                    await sender.Send(command, cancellationToken);
                    return Results.NoContent();
                }
            )
            .WithName("SaveProjectUnitPrices")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Save project unit price flags")
            .WithDescription("Saves location flags (corner, edge, pool view, south, near garden, other) for project units. Works for both Condo and LandAndBuilding project types.")
            .WithTags("Project");
    }
}
