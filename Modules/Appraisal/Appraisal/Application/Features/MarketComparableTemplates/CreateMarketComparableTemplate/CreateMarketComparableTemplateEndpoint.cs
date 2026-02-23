using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.MarketComparableTemplates.CreateMarketComparableTemplate;

public class CreateMarketComparableTemplateEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/market-comparable-templates",
            async (CreateMarketComparableTemplateRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var command = request.Adapt<CreateMarketComparableTemplateCommand>();
                var result = await sender.Send(command, cancellationToken);
                var response = result.Adapt<CreateMarketComparableTemplateResponse>();
                return Results.Created($"/market-comparable-templates/{response.Id}", response);
            })
            .WithName("CreateMarketComparableTemplate")
            .Produces<CreateMarketComparableTemplateResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Create market comparable template")
            .WithDescription("Create a new market comparable template for a property type.")
            .WithTags("MarketComparableTemplates");
    }
}
