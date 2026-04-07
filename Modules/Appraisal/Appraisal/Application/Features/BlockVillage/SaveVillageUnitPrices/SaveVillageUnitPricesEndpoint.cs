namespace Appraisal.Application.Features.BlockVillage.SaveVillageUnitPrices;

public class SaveVillageUnitPricesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/village-unit-prices",
                async (
                    Guid appraisalId,
                    SaveVillageUnitPricesRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<SaveVillageUnitPricesCommand>()
                        with { AppraisalId = appraisalId };

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("SaveVillageUnitPrices")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Save village unit price flags")
            .WithDescription("Updates location flags (corner, edge, near garden, other) for village unit prices.")
            .WithTags("Block Village");
    }
}
