namespace Appraisal.Application.Features.BlockCondo.SaveCondoUnitPrices;

public class SaveCondoUnitPricesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/condo-unit-prices",
                async (
                    Guid appraisalId,
                    SaveCondoUnitPricesRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<SaveCondoUnitPricesCommand>()
                        with { AppraisalId = appraisalId };

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("SaveCondoUnitPrices")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Save condo unit price flags")
            .WithDescription("Updates location flags (corner, edge, pool view, south, other) for condo unit prices.")
            .WithTags("Block Condo");
    }
}
