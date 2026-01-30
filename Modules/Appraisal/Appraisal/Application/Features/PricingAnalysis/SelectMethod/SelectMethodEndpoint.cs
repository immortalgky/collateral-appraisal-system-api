using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.SelectMethod;

public class SelectMethodEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/pricing-analysis/{id}/methods/{methodId}/select",
                async (
                    Guid id,
                    Guid methodId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new SelectMethodCommand(id, methodId);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<SelectMethodResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("SelectMethod")
            .Produces<SelectMethodResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Select method")
            .WithDescription("Selects a method as the primary method, setting all other methods in the same approach as Alternative.")
            .WithTags("PricingAnalysis");
    }
}
