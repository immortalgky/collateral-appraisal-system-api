namespace Collateral.Collateral.Shared.Features.DeleteCollateral;

public class DeleteCollateralEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/collaterals/{id:long}",
                async (long id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new DeleteCollateralCommand(id);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<DeleteCollateralResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("DeleteCollateral")
            .Produces<DeleteCollateralResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete collateral by ID")
            .WithDescription(
                "Deletes a collateral by its ID. If the collateral does not exist, a 404 Not Found error is returned."
            )
            .WithTags("Collateral");
    }
}
