using Carter;
using MediatR;

namespace Appraisal.Application.Features.MarketComparableTemplates.SetMarketComparableTemplateStatus;

public class SetMarketComparableTemplateStatusEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/market-comparable-templates/{id:guid}/activate",
            async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                await sender.Send(new SetMarketComparableTemplateStatusCommand(id, true), cancellationToken);
                return Results.NoContent();
            })
            .WithName("ActivateMarketComparableTemplate")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Activate a market comparable template")
            .WithTags("MarketComparableTemplates");

        app.MapPost("/market-comparable-templates/{id:guid}/deactivate",
            async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                await sender.Send(new SetMarketComparableTemplateStatusCommand(id, false), cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeactivateMarketComparableTemplate")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Deactivate a market comparable template")
            .WithTags("MarketComparableTemplates");
    }
}
