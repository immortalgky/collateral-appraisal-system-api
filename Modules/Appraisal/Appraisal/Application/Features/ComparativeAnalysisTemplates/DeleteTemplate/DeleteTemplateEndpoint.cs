using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.DeleteTemplate;

public class DeleteTemplateEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/comparative-analysis-templates/{id:guid}",
                async (Guid id, ISender sender) =>
                {
                    var command = new DeleteTemplateCommand(id);
                    await sender.Send(command);
                    return Results.NoContent();
                })
            .WithName("DeleteComparativeAnalysisTemplate")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete a comparative analysis template")
            .WithDescription("Soft deletes a template by deactivating it")
            .WithTags("ComparativeAnalysisTemplates");
    }
}
