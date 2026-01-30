using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.UpdateComparableLink;

public class UpdateComparableLinkEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/pricing-analysis/{pricingAnalysisId:guid}/comparable-links/{linkId:guid}",
            async (Guid pricingAnalysisId, Guid linkId, UpdateComparableLinkRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var command = new UpdateComparableLinkCommand(
                    pricingAnalysisId,
                    linkId,
                    request.Weight,
                    request.DisplaySequence);

                var result = await sender.Send(command, cancellationToken);
                var response = result.Adapt<UpdateComparableLinkResponse>();
                return Results.Ok(response);
            })
            .WithName("UpdateComparableLink")
            .Produces<UpdateComparableLinkResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Update comparable link")
            .WithDescription("Updates the weight and/or display sequence of a comparable link.")
            .WithTags("PricingAnalysis");
    }
}
