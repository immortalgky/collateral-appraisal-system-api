using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.LinkComparable;

public class LinkComparableEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/pricing-analysis/{pricingAnalysisId:guid}/methods/{methodId:guid}/comparables",
            async (Guid pricingAnalysisId, Guid methodId, LinkComparableRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var command = new LinkComparableCommand(
                    pricingAnalysisId,
                    methodId,
                    request.MarketComparableId,
                    request.DisplaySequence,
                    request.Weight);

                var result = await sender.Send(command, cancellationToken);
                var response = result.Adapt<LinkComparableResponse>();
                return Results.Created($"/pricing-analysis/{pricingAnalysisId}/comparable-links/{response.LinkId}", response);
            })
            .WithName("LinkComparable")
            .Produces<LinkComparableResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Link comparable to pricing method")
            .WithDescription("Links a market comparable to a pricing method and creates an associated calculation.")
            .WithTags("PricingAnalysis");
    }
}
