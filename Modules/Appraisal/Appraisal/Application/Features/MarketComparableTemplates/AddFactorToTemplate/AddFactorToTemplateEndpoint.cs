using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.MarketComparableTemplates.AddFactorToTemplate;

public class AddFactorToTemplateEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/market-comparable-templates/{templateId:guid}/factors",
            async (Guid templateId, AddFactorToTemplateRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var command = new AddFactorToTemplateCommand(
                    templateId,
                    request.FactorId,
                    request.DisplaySequence,
                    request.IsMandatory);

                var result = await sender.Send(command, cancellationToken);
                var response = result.Adapt<AddFactorToTemplateResponse>();
                return Results.Created($"/market-comparable-templates/{templateId}/factors/{response.TemplateFactorId}", response);
            })
            .WithName("AddFactorToTemplate")
            .Produces<AddFactorToTemplateResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Add factor to template")
            .WithDescription("Link an existing factor to a market comparable template.")
            .WithTags("MarketComparableTemplates");
    }
}
