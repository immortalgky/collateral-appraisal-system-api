namespace Appraisal.Application.Features.PricingAnalysis.ApplySelection;

public class ApplySelectionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/pricing-analysis/{id:guid}/selection",
                async (
                    Guid id,
                    ApplySelectionRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new ApplySelectionCommand(
                        id,
                        request.Selections ?? [],
                        request.FinalApproachId);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<ApplySelectionResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("ApplyPricingSelection")
            .Produces<ApplySelectionResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Apply pricing selection")
            .WithDescription("Applies the primary method for each listed approach AND the analysis's final approach in a single transaction, propagating the final approach's value to FinalAppraisedValue and raising the valuation-summary event exactly once. Supersedes calling SelectMethod per approach followed by SelectApproach; those endpoints remain available.")
            .WithTags("PricingAnalysis");
    }
}
