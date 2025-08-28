using Shared.Pagination;

namespace Collateral.Collateral.Shared.Features.GetCollaterals;

public class GetCollateralEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/collaterals",
                async (
                    [AsParameters] PaginationRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetCollateralQuery(request);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetCollateralResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetCollaterals")
            .Produces<GetCollateralResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get all collaterals")
            .WithDescription(
                "Retrieves all collaterals from the system. This endpoint returns a list of collaterals with their details."
            )
            .WithTags("Collateral");
    }
}
