using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.StartPricingAnalysis;

public class StartPricingAnalysisEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/pricing-analysis/{id}/start",
                async (
                    Guid id,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new StartPricingAnalysisCommand(id);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<StartPricingAnalysisResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("StartPricingAnalysis")
            .Produces<StartPricingAnalysisResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Start pricing analysis")
            .WithDescription("Change pricing analysis status from Draft to InProgress.")
            .WithTags("PricingAnalysis");
    }
}
