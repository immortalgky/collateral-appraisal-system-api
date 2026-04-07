namespace Appraisal.Application.Features.BlockCondo.SaveCondoPricingAssumptions;

public class SaveCondoPricingAssumptionsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/condo-pricing-assumptions",
                async (
                    Guid appraisalId,
                    SaveCondoPricingAssumptionsRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<SaveCondoPricingAssumptionsCommand>()
                        with { AppraisalId = appraisalId };

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<SaveCondoPricingAssumptionsResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("SaveCondoPricingAssumptions")
            .Produces<SaveCondoPricingAssumptionsResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Save condo pricing assumptions")
            .WithDescription("Creates or updates condo pricing assumptions with model assumptions.")
            .WithTags("Block Condo");
    }
}
