using Carter;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.UnlinkComparable;

public class UnlinkComparableEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/pricing-analysis/{pricingAnalysisId:guid}/methods/{methodId:guid}/comparables/{linkId:guid}",
            async (Guid pricingAnalysisId, Guid methodId, Guid linkId, ISender sender, CancellationToken cancellationToken) =>
            {
                var command = new UnlinkComparableCommand(pricingAnalysisId, methodId, linkId);
                var result = await sender.Send(command, cancellationToken);
                return Results.NoContent();
            })
            .WithName("UnlinkComparable")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Unlink comparable from pricing method")
            .WithDescription("Removes a market comparable link and its associated calculation from a pricing method.")
            .WithTags("PricingAnalysis");
    }
}
