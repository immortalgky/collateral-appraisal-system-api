namespace Appraisal.Application.Features.PricingAnalysis.SelectApproach;

public class SelectApproachEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/pricing-analysis/{id:guid}/approaches/{approachId:guid}/select",
                async (
                    Guid id,
                    Guid approachId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new SelectApproachCommand(id, approachId);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<SelectApproachResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("SelectApproach")
            .Produces<SelectApproachResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Select approach")
            .WithDescription("Selects an approach as the final approach for the analysis, unselecting all others, and propagates its value to FinalAppraisedValue. The approach must already have a selected method (see SelectMethod).")
            .WithTags("PricingAnalysis");
    }
}
