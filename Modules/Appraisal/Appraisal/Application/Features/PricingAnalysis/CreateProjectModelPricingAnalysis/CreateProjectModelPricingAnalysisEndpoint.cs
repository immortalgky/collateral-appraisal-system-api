using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.CreateProjectModelPricingAnalysis;

public class CreateProjectModelPricingAnalysisEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/models/{modelId:guid}/pricing-analysis",
                async (
                    Guid modelId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new CreateProjectModelPricingAnalysisCommand(modelId);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateProjectModelPricingAnalysisResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("CreateProjectModelPricingAnalysis")
            .Produces<CreateProjectModelPricingAnalysisResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create project model pricing analysis")
            .WithDescription("Create a new pricing analysis for a project model. The resulting FinalAppraisedValue becomes the model's standard price.")
            .WithTags("PricingAnalysis");
    }
}
