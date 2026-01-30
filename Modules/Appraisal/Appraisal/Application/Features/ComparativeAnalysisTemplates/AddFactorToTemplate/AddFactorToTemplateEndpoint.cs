using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.AddFactorToTemplate;

public class AddFactorToTemplateEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/comparative-analysis-templates/{id:guid}/factors",
                async (Guid id, AddFactorToTemplateRequest request, ISender sender) =>
                {
                    var command = new AddFactorToTemplateCommand(
                        id,
                        request.FactorId,
                        request.DisplaySequence,
                        request.IsMandatory,
                        request.DefaultWeight
                    );

                    var result = await sender.Send(command);
                    return Results.Created($"/comparative-analysis-templates/{id}/factors/{result.TemplateFactorId}", result);
                })
            .WithName("AddFactorToComparativeAnalysisTemplate")
            .Produces<AddFactorToTemplateResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Add a factor to a template")
            .WithDescription("Adds a market comparable factor to the template")
            .WithTags("ComparativeAnalysisTemplates");
    }
}
