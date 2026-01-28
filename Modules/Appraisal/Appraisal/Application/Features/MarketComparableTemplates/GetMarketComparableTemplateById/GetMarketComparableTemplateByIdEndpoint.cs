using Carter;
using MediatR;

namespace Appraisal.Application.Features.MarketComparableTemplates.GetMarketComparableTemplateById;

public class GetMarketComparableTemplateByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/market-comparable-templates/{id:guid}",
            async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                var query = new GetMarketComparableTemplateByIdQuery(id);
                var result = await sender.Send(query, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("GetMarketComparableTemplateById")
            .Produces<GetMarketComparableTemplateByIdResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get market comparable template by ID")
            .WithDescription("Retrieve a specific market comparable template with all its factors.")
            .WithTags("MarketComparableTemplates");
    }
}
