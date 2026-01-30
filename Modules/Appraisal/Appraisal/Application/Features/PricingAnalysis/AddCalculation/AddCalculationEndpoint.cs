using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.AddCalculation;

public class AddCalculationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/pricing-analysis/{pricingAnalysisId:guid}/methods/{methodId:guid}/calculations",
            async (Guid pricingAnalysisId, Guid methodId, AddCalculationRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var command = new AddCalculationCommand(
                    pricingAnalysisId,
                    methodId,
                    request.MarketComparableId);

                var result = await sender.Send(command, cancellationToken);
                var response = result.Adapt<AddCalculationResponse>();
                return Results.Created($"/pricing-analysis/{pricingAnalysisId}/calculations/{response.CalculationId}", response);
            })
            .WithName("AddCalculation")
            .Produces<AddCalculationResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Add calculation to pricing method")
            .WithDescription("Creates a new pricing calculation for a market comparable within a pricing method.")
            .WithTags("PricingAnalysis");
    }
}
