using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.GetTemplateById;

public class GetTemplateByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/comparative-analysis-templates/{id:guid}",
                async (Guid id, ISender sender) =>
                {
                    var query = new GetTemplateByIdQuery(id);
                    var result = await sender.Send(query);
                    return Results.Ok(result);
                })
            .WithName("GetComparativeAnalysisTemplateById")
            .Produces<GetTemplateByIdResult>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get a comparative analysis template by ID")
            .WithDescription("Returns template details with all factors")
            .WithTags("ComparativeAnalysisTemplates");
    }
}
