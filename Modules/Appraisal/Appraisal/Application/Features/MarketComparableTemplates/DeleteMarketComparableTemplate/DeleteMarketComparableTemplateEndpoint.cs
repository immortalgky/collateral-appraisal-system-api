using Carter;
using MediatR;

namespace Appraisal.Application.Features.MarketComparableTemplates.DeleteMarketComparableTemplate;

public class DeleteMarketComparableTemplateEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/market-comparable-templates/{id:guid}",
            async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                var command = new DeleteMarketComparableTemplateCommand(id);
                var result = await sender.Send(command, cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeleteMarketComparableTemplate")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete market comparable template")
            .WithDescription("Soft delete (deactivate) a market comparable template.")
            .WithTags("MarketComparableTemplates");
    }
}
