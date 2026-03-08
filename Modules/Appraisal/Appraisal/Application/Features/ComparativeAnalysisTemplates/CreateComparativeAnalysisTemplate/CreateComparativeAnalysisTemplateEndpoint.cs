using Carter;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.CreateComparativeAnalysisTemplate;

public class CreateComparativeAnalysisTemplateEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/comparative-analysis-templates",
                async (CreateComparativeAnalysisTemplateRequest request, ISender sender) =>
                {
                    var command = new CreateComparativeAnalysisTemplateCommand(
                        request.TemplateCode,
                        request.TemplateName,
                        request.PropertyType,
                        request.Description
                    );

                    var result = await sender.Send(command);
                    var response = result.Adapt<CreateComparativeAnalysisTemplateResponse>();
                    return Results.Created($"/comparative-analysis-templates/{response.TemplateId}", response);
                })
            .WithName("CreateComparativeAnalysisTemplate")
            .Produces<CreateComparativeAnalysisTemplateResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Create a comparative analysis template")
            .WithDescription("Creates a new template for comparative analysis factors based on property type")
            .WithTags("ComparativeAnalysisTemplates");
    }
}
