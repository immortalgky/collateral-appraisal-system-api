namespace Appraisal.Application.Features.BlockCondo.GetCondoUnitPrices;

public class GetCondoUnitPricesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/condo-unit-prices",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetCondoUnitPricesQuery(appraisalId);
                    var result = await sender.Send(query, cancellationToken);
                    var response = result.Adapt<GetCondoUnitPricesResponse>();
                    return Results.Ok(response);
                }
            )
            .WithName("GetCondoUnitPrices")
            .Produces<GetCondoUnitPricesResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get condo unit prices")
            .WithDescription("Retrieves calculated condo unit prices with unit display data.")
            .WithTags("Block Condo");
    }
}
