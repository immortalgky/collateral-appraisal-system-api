using Carter;
using MediatR;

namespace Appraisal.Application.Features.MarketComparableTemplates.ReorderTemplateFactors;

public class ReorderTemplateFactorsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/market-comparable-templates/{templateId:guid}/factors/reorder",
            async (Guid templateId, ReorderTemplateFactorsRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var command = new ReorderTemplateFactorsCommand(templateId, request.Factors);
                await sender.Send(command, cancellationToken);
                return Results.NoContent();
            })
            .WithName("ReorderTemplateFactors")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Reorder template factors")
            .WithDescription("Persist the display order of factors within a market comparable template.")
            .WithTags("MarketComparableTemplates");
    }
}
