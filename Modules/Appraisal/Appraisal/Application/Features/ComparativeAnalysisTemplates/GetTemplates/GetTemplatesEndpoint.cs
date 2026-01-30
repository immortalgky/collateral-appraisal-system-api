using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.GetTemplates;

public class GetTemplatesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/comparative-analysis-templates",
                async (bool? activeOnly, ISender sender) =>
                {
                    var query = new GetTemplatesQuery(activeOnly ?? false);
                    var result = await sender.Send(query);
                    return Results.Ok(result.Templates);
                })
            .WithName("GetComparativeAnalysisTemplates")
            .Produces<IReadOnlyList<TemplateDto>>()
            .WithSummary("Get all comparative analysis templates")
            .WithDescription("Returns all templates, optionally filtered to only active ones")
            .WithTags("ComparativeAnalysisTemplates");
    }
}
