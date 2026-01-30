using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.GetPricingAnalysis;

public class GetPricingAnalysisEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/pricing-analysis/{id}",
                async (
                    Guid id,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetPricingAnalysisQuery(id);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetPricingAnalysisResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetPricingAnalysis")
            .Produces<GetPricingAnalysisResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get pricing analysis")
            .WithDescription("Get a pricing analysis by ID with all approaches.")
            .WithTags("PricingAnalysis");
    }
}
