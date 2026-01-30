using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.CompletePricingAnalysis;

public class CompletePricingAnalysisEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/pricing-analysis/{id}/complete",
                async (
                    Guid id,
                    CompletePricingAnalysisRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new CompletePricingAnalysisCommand(
                        id,
                        request.MarketValue,
                        request.AppraisedValue,
                        request.ForcedSaleValue
                    );

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CompletePricingAnalysisResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("CompletePricingAnalysis")
            .Produces<CompletePricingAnalysisResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Complete pricing analysis")
            .WithDescription("Complete pricing analysis and set final values (changes status to Completed).")
            .WithTags("PricingAnalysis");
    }
}
