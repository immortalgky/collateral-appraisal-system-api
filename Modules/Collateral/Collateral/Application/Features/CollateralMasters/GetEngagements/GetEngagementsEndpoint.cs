namespace Collateral.Application.Features.CollateralMasters.GetEngagements;

/// <summary>
/// GET /collateral-masters/{id}/engagements?page=&amp;pageSize=
/// Authenticated.
/// </summary>
public class GetEngagementsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/collateral-masters/{id:guid}/engagements",
                async (
                    Guid id,
                    [AsParameters] PaginationRequest paginationRequest,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetEngagementsQuery(id, paginationRequest);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                }
            )
            .WithName("GetCollateralEngagements")
            .Produces<GetEngagementsResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Get engagement list for a collateral master")
            .WithDescription("Returns paginated engagement metadata for the specified collateral master, ordered by appraisal date descending.")
            .WithTags("CollateralMaster")
            .RequireAuthorization();
    }
}
