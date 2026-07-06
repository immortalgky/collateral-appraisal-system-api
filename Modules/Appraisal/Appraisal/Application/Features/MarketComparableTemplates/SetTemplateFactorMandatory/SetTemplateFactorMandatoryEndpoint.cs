using Carter;
using MediatR;

namespace Appraisal.Application.Features.MarketComparableTemplates.SetTemplateFactorMandatory;

public class SetTemplateFactorMandatoryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/market-comparable-templates/{templateId:guid}/factors/{factorId:guid}/mandatory",
            async (Guid templateId, Guid factorId, SetTemplateFactorMandatoryRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var command = new SetTemplateFactorMandatoryCommand(templateId, factorId, request.IsMandatory);
                await sender.Send(command, cancellationToken);
                return Results.NoContent();
            })
            .WithName("SetTemplateFactorMandatory")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Set whether a template factor is mandatory")
            .WithDescription("Update a factor's mandatory flag within a market comparable template without altering its display order.")
            .WithTags("MarketComparableTemplates");
    }
}
