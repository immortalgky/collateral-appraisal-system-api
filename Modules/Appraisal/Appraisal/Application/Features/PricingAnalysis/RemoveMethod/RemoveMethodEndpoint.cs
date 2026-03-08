using Carter;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.RemoveMethod;

public class RemoveMethodEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/pricing-analysis/{id:guid}/approaches/{approachId:guid}/methods/{methodId:guid}",
                async (Guid id, Guid approachId, Guid methodId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new RemoveMethodCommand(id, approachId, methodId);
                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("RemoveMethod")
            .Produces<RemoveMethodResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Remove a pricing method")
            .WithDescription("Removes a pricing method and all its child data (factors, scores, calculations, comparable links, final value, RSQ result). If the method was selected, clears approach selection and final appraised value.")
            .WithTags("PricingAnalysis");
    }
}
