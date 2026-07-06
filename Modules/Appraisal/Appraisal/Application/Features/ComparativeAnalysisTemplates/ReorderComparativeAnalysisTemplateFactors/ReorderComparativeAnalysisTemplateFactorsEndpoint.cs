using Carter;
using MediatR;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.ReorderComparativeAnalysisTemplateFactors;

public class ReorderComparativeAnalysisTemplateFactorsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/comparative-analysis-templates/{templateId:guid}/factors/reorder",
            async (Guid templateId, ReorderComparativeAnalysisTemplateFactorsRequest request, ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new ReorderComparativeAnalysisTemplateFactorsCommand(templateId, request.Factors);
                await sender.Send(command, cancellationToken);
                return Results.NoContent();
            })
            .WithName("ReorderComparativeAnalysisTemplateFactors")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Reorder comparative analysis template factors")
            .WithDescription("Persist the display order of factors within a comparative analysis template.")
            .WithTags("ComparativeAnalysisTemplates");
    }
}
