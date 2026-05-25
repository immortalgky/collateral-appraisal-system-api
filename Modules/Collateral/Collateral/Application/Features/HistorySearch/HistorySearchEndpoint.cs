namespace Collateral.Application.Features.HistorySearch;

/// <summary>
/// POST /history-search
///
/// Single endpoint that returns geo-filtered green (Collateral) and blue (MarketComparable)
/// pins for the History Search (pin) feature in LHB FSD Common Administration.
///
/// Visibility is enforced server-side:
///   - Internal users receive both pin sets.
///   - External users receive only their own company's MarketComparable pins;
///     the Collateral result is always an empty page.
/// </summary>
public class HistorySearchEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/history-search",
                async (
                    HistorySearchQuery query,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("HistorySearch")
            .Produces<HistorySearchResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("History Search (pin)")
            .WithDescription(
                "Returns geo-filtered CollateralMaster (green) and MarketComparable (blue) pins " +
                "within the specified radius of a centre point. External users receive only their " +
                "own company's blue pins; the green pin set is always empty for them.")
            .WithTags("HistorySearch")
            .RequireAuthorization("history-search.view");
    }
}
