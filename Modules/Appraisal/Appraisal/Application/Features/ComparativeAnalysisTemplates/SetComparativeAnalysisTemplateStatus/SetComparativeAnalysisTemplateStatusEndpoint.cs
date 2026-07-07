using Carter;
using MediatR;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.SetComparativeAnalysisTemplateStatus;

public class SetComparativeAnalysisTemplateStatusEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/comparative-analysis-templates/{id:guid}/activate",
            async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                await sender.Send(new SetComparativeAnalysisTemplateStatusCommand(id, true), cancellationToken);
                return Results.NoContent();
            })
            .WithName("ActivateComparativeAnalysisTemplate")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Activate a comparative analysis template")
            .WithTags("ComparativeAnalysisTemplates");

        app.MapPost("/comparative-analysis-templates/{id:guid}/deactivate",
            async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                await sender.Send(new SetComparativeAnalysisTemplateStatusCommand(id, false), cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeactivateComparativeAnalysisTemplate")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Deactivate a comparative analysis template")
            .WithTags("ComparativeAnalysisTemplates");
    }
}
