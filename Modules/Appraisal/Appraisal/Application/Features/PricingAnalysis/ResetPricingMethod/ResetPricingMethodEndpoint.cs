using Carter;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.ResetPricingMethod;

public class ResetPricingMethodEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/pricing-analysis/{id:guid}/methods/{methodId:guid}/reset",
                async (Guid id, Guid methodId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new ResetPricingMethodCommand(id, methodId);
                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("ResetPricingMethod")
            .Produces<ResetPricingMethodResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Reset pricing method")
            .WithDescription("Clears all data (factors, scores, calculations, comparable links, final value, RSQ result) for a pricing method.")
            .WithTags("PricingAnalysis");
    }
}
