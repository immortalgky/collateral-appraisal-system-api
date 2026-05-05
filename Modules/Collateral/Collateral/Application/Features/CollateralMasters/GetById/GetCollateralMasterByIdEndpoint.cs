namespace Collateral.Application.Features.CollateralMasters.GetById;

/// <summary>
/// GET /collateral-masters/{id}
/// Authenticated.
/// </summary>
public class GetCollateralMasterByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/collateral-masters/{id:guid}",
                async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var query = new GetCollateralMasterByIdQuery(id);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                }
            )
            .WithName("GetCollateralMasterById")
            .Produces<GetCollateralMasterByIdResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Get collateral master by ID")
            .WithDescription("Returns the full collateral master detail including type-specific fields.")
            .WithTags("CollateralMaster")
            .RequireAuthorization();
    }
}
