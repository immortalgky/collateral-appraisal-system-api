using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.CreateTemplate;

public class CreateTemplateEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/comparative-analysis-templates",
                async (CreateTemplateRequest request, ISender sender) =>
                {
                    var command = new CreateTemplateCommand(
                        request.TemplateCode,
                        request.TemplateName,
                        request.PropertyType,
                        request.Description
                    );

                    var result = await sender.Send(command);
                    return Results.Created($"/comparative-analysis-templates/{result.TemplateId}", result);
                })
            .WithName("CreateComparativeAnalysisTemplate")
            .Produces<CreateTemplateResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Create a comparative analysis template")
            .WithDescription("Creates a new template for comparative analysis factors based on property type")
            .WithTags("ComparativeAnalysisTemplates");
    }
}
