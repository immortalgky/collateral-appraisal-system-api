using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.UpdatePricingAnalysis;

public class UpdatePricingAnalysisEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/pricing-analysis/{id}",
                async (
                    Guid id,
                    UpdatePricingAnalysisRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new UpdatePricingAnalysisCommand(
                        id,
                        request.MarketValue,
                        request.AppraisedValue,
                        request.ForcedSaleValue
                    );

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<UpdatePricingAnalysisResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("UpdatePricingAnalysis")
            .Produces<UpdatePricingAnalysisResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update pricing analysis")
            .WithDescription("Update final values for a pricing analysis.")
            .WithTags("PricingAnalysis");
    }
}
