namespace Collateral.Collateral.Shared.Features.GetCollateralById;

public class GetCollateralByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/collaterals/{id:long}",
                async (long id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new GetCollateralByIdQuery(id);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<GetCollateralByIdResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetCollateralById")
            .Produces<GetCollateralByIdResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get collateral by ID")
            .WithDescription("Get collateral by ID")
            .WithTags("Collateral");
    }
}
