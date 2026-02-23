using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.RemoveFactorFromTemplate;

public class RemoveFactorFromTemplateEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/comparative-analysis-templates/{id:guid}/factors/{factorId:guid}",
                async (Guid id, Guid factorId, ISender sender) =>
                {
                    var command = new RemoveFactorFromTemplateCommand(id, factorId);
                    await sender.Send(command);
                    return Results.NoContent();
                })
            .WithName("RemoveFactorFromComparativeAnalysisTemplate")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Remove a factor from a template")
            .WithDescription("Removes a market comparable factor from the template")
            .WithTags("ComparativeAnalysisTemplates");
    }
}
