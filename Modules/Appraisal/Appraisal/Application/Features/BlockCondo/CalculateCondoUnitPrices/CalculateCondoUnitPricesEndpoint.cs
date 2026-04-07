namespace Appraisal.Application.Features.BlockCondo.CalculateCondoUnitPrices;

public class CalculateCondoUnitPricesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/condo-unit-prices/calculate",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new CalculateCondoUnitPricesCommand(appraisalId);
                    await sender.Send(command, cancellationToken);
                    return Results.Ok();
                }
            )
            .WithName("CalculateCondoUnitPrices")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Calculate condo unit prices")
            .WithDescription("Calculates appraisal values for all condo units based on pricing assumptions.")
            .WithTags("Block Condo");
    }
}
