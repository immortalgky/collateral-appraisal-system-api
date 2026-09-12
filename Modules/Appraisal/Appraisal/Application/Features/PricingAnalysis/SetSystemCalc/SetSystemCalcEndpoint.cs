using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.SetSystemCalc;

public class SetSystemCalcEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/pricing-analysis/{id}/system-calc",
                async (
                    Guid id,
                    SetSystemCalcRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new SetSystemCalcCommand(id, request.UseSystemCalc);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<SetSystemCalcResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("SetPricingAnalysisSystemCalc")
            .Produces<SetSystemCalcResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Set analysis-wide system-calculation mode")
            .WithDescription("Sets PricingAnalysis.UseSystemCalc (true = system calculation, false = manual). Does not modify final values.")
            .WithTags("PricingAnalysis");
    }
}