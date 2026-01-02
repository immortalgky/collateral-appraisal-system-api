namespace Collateral.Collateral.Shared.Features.CreateCollateral;

public class CreateCollateralEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/collaterals",
                async (
                    CreateCollateralRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<CreateCollateralCommand>();

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateCollateralResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("CreateCollateral")
            .Produces<CreateCollateralResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Create a collateral")
            .WithDescription(
                "Creates a new collateral in the system. The collateral details are provided in the collateral body."
            )
            .WithTags("Collateral");
    }
}
