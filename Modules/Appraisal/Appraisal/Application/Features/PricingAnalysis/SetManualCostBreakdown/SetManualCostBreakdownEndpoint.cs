using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.SetManualCostBreakdown;

/// <summary>
/// Endpoint: POST /pricing-analysis/{id}/methods/{methodId}/manual-cost-breakdown
/// Records a hand-entered Cost-approach land rate and rounded price.
/// </summary>
public class SetManualCostBreakdownEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/pricing-analysis/{id:guid}/methods/{methodId:guid}/manual-cost-breakdown",
                async (
                    Guid id,
                    Guid methodId,
                    SetManualCostBreakdownRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new SetManualCostBreakdownCommand(
                        id,
                        methodId,
                        request.LandRatePerSqWa,
                        request.IndicatedValue
                    );

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<SetManualCostBreakdownResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("SetManualCostBreakdown")
            .Produces<SetManualCostBreakdownResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Record a manual Cost-approach land breakdown")
            .WithDescription(
                "Stores the appraiser's land rate per square wa and rounded land price on a Cost-approach land "
                + "method. Land area is taken from the group's title deeds. The method prices land only; the "
                + "building is the Building Cost method's own line. A null rate clears the breakdown, 0 is a real "
                + "zero land value, and a negative rate or price is rejected.")
            .WithTags("PricingAnalysis");
    }
}
