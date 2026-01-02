namespace Collateral.Collateral.Shared.Features.UpdateCollateralEngagement;

public class UpdateCollateralEngagementEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch(
                "/collaterals/{id:long}/engagements",
                async (
                    UpdateCollateralEngagementRequest request,
                    long id,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<UpdateCollateralEngagementCommand>() with
                    {
                        CollatId = id,
                    };

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<UpdateCollateralEngagementResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("UpdateCollateralEngagement")
            .Produces<UpdateCollateralEngagementResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Set an active request ID of the collateral")
            .WithDescription("Set an active request ID of the collateral.")
            .WithTags("Collateral");
    }
}
