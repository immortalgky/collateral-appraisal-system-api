using Appraisal.Application.Features.PricingAnalysis.GetReferences;
using Carter;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.GetGroupReferences;

public class GetGroupReferencesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/pricing-analysis/{pricingAnalysisId:guid}/references",
                async (
                    Guid pricingAnalysisId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new GetGroupReferencesQuery(pricingAnalysisId), cancellationToken);

                    return Results.Ok(new GetGroupReferencesResponse(result.References));
                }
            )
            .WithName("GetGroupReferences")
            .Produces<GetGroupReferencesResponse>(StatusCodes.Status200OK)
            .WithSummary("List all market references for a property group")
            .WithDescription(
                "Returns every saved reference PricingAnalysis hosted by a method in the given group's " +
                "PricingAnalysis (HostMethodId scope). Powers the group-level References section.")
            .WithTags("PricingAnalysis");
    }
}
