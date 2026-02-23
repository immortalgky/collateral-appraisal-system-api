using Carter;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Appraisal.Application.Features.PricingAnalysis.GetComparativeFactors;

public class GetComparativeFactorsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/pricing-analysis/{id:guid}/methods/{methodId:guid}/comparative-factors",
                async (Guid id, Guid methodId, ISender sender) =>
                {
                    var query = new GetComparativeFactorsQuery(id, methodId);
                    var result = await sender.Send(query);
                    var response = result.Adapt<GetComparativeFactorsResponse>();
                    return Results.Ok(response);
                })
            .WithName("GetComparativeFactors")
            .Produces<GetComparativeFactorsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get comparative factors for a pricing method")
            .WithDescription("Returns Step 1 factor selections, Step 2 factor scores, and calculations")
            .WithTags("PricingAnalysis");
    }
}
