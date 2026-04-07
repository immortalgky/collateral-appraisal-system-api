namespace Appraisal.Application.Features.BlockCondo.GetCondoPricingAssumptions;

public class GetCondoPricingAssumptionsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/condo-pricing-assumptions",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetCondoPricingAssumptionsQuery(appraisalId);
                    var result = await sender.Send(query, cancellationToken);
                    var response = result.Adapt<GetCondoPricingAssumptionsResponse>();
                    return Results.Ok(response);
                }
            )
            .WithName("GetCondoPricingAssumptions")
            .Produces<GetCondoPricingAssumptionsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get condo pricing assumptions")
            .WithDescription("Retrieves condo pricing assumptions with model assumptions for an appraisal.")
            .WithTags("Block Condo");
    }
}
