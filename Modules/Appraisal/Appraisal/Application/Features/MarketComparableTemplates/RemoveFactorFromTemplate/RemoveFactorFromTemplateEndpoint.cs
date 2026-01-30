using Carter;
using MediatR;

namespace Appraisal.Application.Features.MarketComparableTemplates.RemoveFactorFromTemplate;

public class RemoveFactorFromTemplateEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/market-comparable-templates/{templateId:guid}/factors/{factorId:guid}",
            async (Guid templateId, Guid factorId, ISender sender, CancellationToken cancellationToken) =>
            {
                var command = new RemoveFactorFromTemplateCommand(templateId, factorId);
                var result = await sender.Send(command, cancellationToken);
                return Results.NoContent();
            })
            .WithName("RemoveFactorFromTemplate")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Remove factor from template")
            .WithDescription("Remove a comparison factor from a market comparable template.")
            .WithTags("MarketComparableTemplates");
    }
}
