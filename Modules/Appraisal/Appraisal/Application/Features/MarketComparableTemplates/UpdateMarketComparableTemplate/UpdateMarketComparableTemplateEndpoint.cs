using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.MarketComparableTemplates.UpdateMarketComparableTemplate;

public class UpdateMarketComparableTemplateEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/market-comparable-templates/{id:guid}",
            async (Guid id, UpdateMarketComparableTemplateRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var command = new UpdateMarketComparableTemplateCommand(
                    id,
                    request.TemplateCode,
                    request.TemplateName,
                    request.PropertyType,
                    request.Description);

                var result = await sender.Send(command, cancellationToken);
                var response = result.Adapt<UpdateMarketComparableTemplateResponse>();
                return Results.Ok(response);
            })
            .WithName("UpdateMarketComparableTemplate")
            .Produces<UpdateMarketComparableTemplateResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Update market comparable template")
            .WithDescription("Update an existing market comparable template details.")
            .WithTags("MarketComparableTemplates");
    }
}
