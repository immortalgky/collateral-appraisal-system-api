using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.UpdateFactorInTemplate;

public class UpdateFactorInTemplateEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/comparative-analysis-templates/{templateId:guid}/factors/{factorId:guid}",
                async (Guid templateId, Guid factorId, UpdateFactorInTemplateRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new UpdateFactorInTemplateCommand(
                        templateId,
                        factorId,
                        request.IsMandatory,
                        request.DefaultWeight,
                        request.DefaultIntensity,
                        request.IsCalculationFactor
                    );

                    await sender.Send(command, cancellationToken);
                    return Results.NoContent();
                })
            .WithName("UpdateFactorInComparativeAnalysisTemplate")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update a factor within a template")
            .WithDescription("Updates a factor's mandatory / calculation / default weight / default intensity in place without altering its display order.")
            .WithTags("ComparativeAnalysisTemplates");
    }
}
