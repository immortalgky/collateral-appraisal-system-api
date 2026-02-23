using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.UpdateMethod;

public class UpdateMethodEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/pricing-analysis/{id}/methods/{methodId}",
                async (
                    Guid id,
                    Guid methodId,
                    UpdateMethodRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new UpdateMethodCommand(
                        id,
                        methodId,
                        request.MethodValue,
                        request.ValuePerUnit,
                        request.UnitType);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<UpdateMethodResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("UpdateMethod")
            .Produces<UpdateMethodResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update method")
            .WithDescription("Updates the value, value per unit, and/or unit type of an existing method.")
            .WithTags("PricingAnalysis");
    }
}
