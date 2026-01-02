namespace Collateral.Collateral.Shared.Features.UpdateCollateral;

public class UpdateCollateralEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch(
                "/collaterals/{id:long}",
                async (
                    long id,
                    UpdateCollateralRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<UpdateCollateralCommand>() with { Id = id };

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<UpdateCollateralResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("UpdateCollateral")
            .Produces<UpdateCollateralResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update an existing collateral")
            .WithDescription(
                "Updates an existing collateral in the system. The collateral details are provided in the request body."
            )
            .WithTags("Collateral");
    }
}
