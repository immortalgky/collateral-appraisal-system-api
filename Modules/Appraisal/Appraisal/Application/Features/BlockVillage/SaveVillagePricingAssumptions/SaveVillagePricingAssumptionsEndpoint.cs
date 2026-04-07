namespace Appraisal.Application.Features.BlockVillage.SaveVillagePricingAssumptions;

public class SaveVillagePricingAssumptionsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/village-pricing-assumptions",
                async (
                    Guid appraisalId,
                    SaveVillagePricingAssumptionsRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<SaveVillagePricingAssumptionsCommand>()
                        with { AppraisalId = appraisalId };

                    var result = await sender.Send(command, cancellationToken);

                    return Results.Ok(result.Adapt<SaveVillagePricingAssumptionsResponse>());
                }
            )
            .WithName("SaveVillagePricingAssumptions")
            .Produces<SaveVillagePricingAssumptionsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Save village pricing assumptions")
            .WithDescription("Creates or updates pricing assumptions for village units.")
            .WithTags("Block Village");
    }
}
