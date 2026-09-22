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
                        request.UnitType,
                        request.Remark,
                        request.UseSystemCalc,
                        request.Role);

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
            .WithDescription(
                "Updates the value, value per unit, unit type, remark, calc mode, and/or role of an existing method. " +
                "Remark: omit/null leaves it unchanged, empty string clears it, non-empty sets it. " +
                "UseSystemCalc: omit/null leaves it unchanged; true/false sets the method's calc mode and clears its recorded value. " +
                "Role: omit/null leaves it unchanged.")
            .WithTags("PricingAnalysis");
    }
}
