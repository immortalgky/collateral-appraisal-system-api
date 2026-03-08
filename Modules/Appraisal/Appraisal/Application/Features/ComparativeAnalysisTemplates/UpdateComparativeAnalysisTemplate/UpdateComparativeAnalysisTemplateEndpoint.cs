using Carter;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.UpdateComparativeAnalysisTemplate;

public class UpdateComparativeAnalysisTemplateEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/comparative-analysis-templates/{id:guid}",
                async (Guid id, UpdateComparativeAnalysisTemplateRequest request, ISender sender) =>
                {
                    var command = new UpdateComparativeAnalysisTemplateCommand(
                        id,
                        request.TemplateName,
                        request.Description,
                        request.IsActive
                    );

                    var result = await sender.Send(command);
                    var response = result.Adapt<UpdateComparativeAnalysisTemplateResponse>();
                    return Results.Ok(response);
                })
            .WithName("UpdateComparativeAnalysisTemplate")
            .Produces<UpdateComparativeAnalysisTemplateResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update a comparative analysis template")
            .WithDescription("Updates template name, description, and active status")
            .WithTags("ComparativeAnalysisTemplates");
    }
}
