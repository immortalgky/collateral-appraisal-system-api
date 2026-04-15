using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.InitializeIncomeAnalysis;

public class InitializeIncomeAnalysisEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/pricing-analysis/{pricingAnalysisId:guid}/methods/{methodId:guid}/income-analysis:initialize",
                async (Guid pricingAnalysisId, Guid methodId,
                    InitializeIncomeAnalysisRequest request,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = new InitializeIncomeAnalysisCommand(
                        pricingAnalysisId,
                        methodId,
                        request.TemplateCode
                    );

                    var result = await sender.Send(command, cancellationToken);
                    var response = result.Adapt<InitializeIncomeAnalysisResponse>();
                    return Results.Ok(response);
                })
            .WithName("InitializeIncomeAnalysis")
            .Produces<InitializeIncomeAnalysisResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Initialize income analysis from template")
            .WithDescription(
                "Clones a PricingTemplate into a new IncomeAnalysis for an Income method. " +
                "Returns 409 Conflict if an analysis already exists — use PUT to replace it.")
            .WithTags("PricingAnalysis");
    }
}
