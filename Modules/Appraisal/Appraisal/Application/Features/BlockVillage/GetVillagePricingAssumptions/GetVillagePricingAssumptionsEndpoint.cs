namespace Appraisal.Application.Features.BlockVillage.GetVillagePricingAssumptions;

public class GetVillagePricingAssumptionsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/village-pricing-assumptions",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(new GetVillagePricingAssumptionsQuery(appraisalId), cancellationToken);
                    return Results.Ok(result.Adapt<GetVillagePricingAssumptionsResponse>());
                }
            )
            .WithName("GetVillagePricingAssumptions")
            .Produces<GetVillagePricingAssumptionsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get village pricing assumptions")
            .WithDescription("Gets the pricing assumptions for village units.")
            .WithTags("Block Village");
    }
}
