namespace Appraisal.Application.Features.BlockVillage.GetVillageUnitPrices;

public class GetVillageUnitPricesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/village-unit-prices",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetVillageUnitPricesQuery(appraisalId);
                    var result = await sender.Send(query, cancellationToken);
                    var response = result.Adapt<GetVillageUnitPricesResponse>();
                    return Results.Ok(response);
                }
            )
            .WithName("GetVillageUnitPrices")
            .Produces<GetVillageUnitPricesResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get village unit prices")
            .WithDescription("Retrieves calculated village unit prices with unit display data.")
            .WithTags("Block Village");
    }
}
