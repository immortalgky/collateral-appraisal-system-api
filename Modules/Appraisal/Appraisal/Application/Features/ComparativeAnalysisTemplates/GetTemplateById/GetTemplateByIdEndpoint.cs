using Carter;
using Mapster;
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
                    var response = result.Adapt<GetTemplateByIdResponse>();
                    return Results.Ok(response);
                })
            .WithName("GetComparativeAnalysisTemplateById")
            .Produces<GetTemplateByIdResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get a comparative analysis template by ID")
            .WithDescription("Returns template details with all factors")
            .WithTags("ComparativeAnalysisTemplates");
    }
}
