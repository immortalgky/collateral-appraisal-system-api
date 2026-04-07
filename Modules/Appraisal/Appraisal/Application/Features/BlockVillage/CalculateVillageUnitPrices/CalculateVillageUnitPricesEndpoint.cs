namespace Appraisal.Application.Features.BlockVillage.CalculateVillageUnitPrices;

public class CalculateVillageUnitPricesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/village-unit-prices/calculate",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new CalculateVillageUnitPricesCommand(appraisalId);
                    await sender.Send(command, cancellationToken);
                    return Results.Ok();
                }
            )
            .WithName("CalculateVillageUnitPrices")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Calculate village unit prices")
            .WithDescription("Calculates appraisal values for all village units based on pricing assumptions.")
            .WithTags("Block Village");
    }
}
