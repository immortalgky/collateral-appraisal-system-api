using Carter;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Pagination;

namespace Appraisal.Application.Features.MarketComparables.GetMarketComparables;

public class GetMarketComparablesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/market-comparables",
                async (
                    [AsParameters] PaginationRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetMarketComparablesQuery(request);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetMarketComparablesResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetMarketComparables")
            .Produces<GetMarketComparablesResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get all market comparables")
            .WithDescription("Retrieves all market comparable records with pagination support.")
            .WithTags("MarketComparable");
    }
}