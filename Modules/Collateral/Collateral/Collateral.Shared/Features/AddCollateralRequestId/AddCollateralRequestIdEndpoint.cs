namespace Collateral.Collateral.Shared.Features.AddCollateralRequestId;

public class AddCollateralRequestIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/collaterals/{id:long}/req-ids",
                async (
                    AddCollateralRequestIdRequest request,
                    long id,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<AddCollateralRequestIdCommand>() with
                    {
                        CollatId = id,
                    };

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<AddCollateralRequestIdResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("AddCollateralRequestId")
            .Produces<AddCollateralRequestIdResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Add new request ID to collateral")
            .WithDescription("Add a new request ID to collateral.")
            .WithTags("Collateral");
    }
}
