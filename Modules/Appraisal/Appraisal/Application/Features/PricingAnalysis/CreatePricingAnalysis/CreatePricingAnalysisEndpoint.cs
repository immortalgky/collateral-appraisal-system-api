using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.CreatePricingAnalysis;

public class CreatePricingAnalysisEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/property-groups/{groupId}/pricing-analysis",
                async (
                    Guid groupId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new CreatePricingAnalysisCommand(groupId);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreatePricingAnalysisResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("CreatePricingAnalysis")
            .Produces<CreatePricingAnalysisResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create pricing analysis")
            .WithDescription("Create a new pricing analysis for a property group.")
            .WithTags("PricingAnalysis");
    }
}
