namespace Appraisal.Application.Features.PricingAnalysis.PreviewIncomeAnalysis;

public class PreviewIncomeAnalysisEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/pricing-analysis/{pricingAnalysisId:guid}/methods/{methodId:guid}/income-analysis:preview",
                async (Guid pricingAnalysisId, Guid methodId,
                    PreviewIncomeAnalysisRequest request,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = new PreviewIncomeAnalysisCommand(
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
                    return Results.Ok(result);
                })
            .WithName("PreviewIncomeAnalysis")
            .Produces<PreviewIncomeAnalysisResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Preview income analysis (no persistence)")
            .WithDescription(
                "Runs the exact same calculation as Save but never writes to the database. " +
                "Use this to display server-authoritative computed values while the user is editing the form.")
            .WithTags("PricingAnalysis");
    }
}
