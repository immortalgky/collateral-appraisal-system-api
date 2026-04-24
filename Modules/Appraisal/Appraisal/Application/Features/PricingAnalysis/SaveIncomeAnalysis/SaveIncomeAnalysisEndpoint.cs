using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.SaveIncomeAnalysis;

public class SaveIncomeAnalysisEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/pricing-analysis/{pricingAnalysisId:guid}/methods/{methodId:guid}/income-analysis",
                async (Guid pricingAnalysisId, Guid methodId,
                    SaveIncomeAnalysisRequest request,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = new SaveIncomeAnalysisCommand(
                        pricingAnalysisId,
                        methodId,
                        request.AppraisalId,
                        request.PropertyId,
                        request.TemplateCode,
                        request.TemplateName,
                        request.TotalNumberOfYears,
                        request.TotalNumberOfDayInYear,
                        request.CapitalizeRate,
                        request.DiscountedRate,
                        request.Sections,
                        request.FinalValueAdjust,
                        request.IsHighestBestUsed,
                        request.HighestBestUsed,
                        request.AppraisalPriceRounded
                    );

                    var result = await sender.Send(command, cancellationToken);
                    var response = result.Adapt<SaveIncomeAnalysisResponse>();
                    return Results.Ok(response);
                })
            .WithName("SaveIncomeAnalysis")
            .Produces<SaveIncomeAnalysisResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Save income analysis")
            .WithDescription(
                "Full-replace upsert for income-approach analysis. " +
                "Validates all method detail shapes, recalculates server-side, " +
                "and returns the canonical IncomeAnalysis tree.")
            .WithTags("PricingAnalysis");
    }
}
