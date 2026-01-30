using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.GetPricingAnalysisByGroup;

public class GetPricingAnalysisByGroupEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/property-groups/{groupId}/pricing-analysis",
                async (
                    Guid groupId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetPricingAnalysisByGroupQuery(groupId);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetPricingAnalysisByGroupResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetPricingAnalysisByGroup")
            .Produces<GetPricingAnalysisByGroupResponse>(StatusCodes.Status200OK)
            .WithSummary("Get pricing analysis by property group")
            .WithDescription("Get pricing analysis for a specific property group.")
            .WithTags("PricingAnalysis");
    }
}
