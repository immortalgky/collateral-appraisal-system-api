using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.UpdateTemplate;

public class UpdateTemplateEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/comparative-analysis-templates/{id:guid}",
                async (Guid id, UpdateTemplateRequest request, ISender sender) =>
                {
                    var command = new UpdateTemplateCommand(
                        id,
                        request.TemplateName,
                        request.Description,
                        request.IsActive
                    );

                    var result = await sender.Send(command);
                    return Results.Ok(result);
                })
            .WithName("UpdateComparativeAnalysisTemplate")
            .Produces<UpdateTemplateResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update a comparative analysis template")
            .WithDescription("Updates template name, description, and active status")
            .WithTags("ComparativeAnalysisTemplates");
    }
}
