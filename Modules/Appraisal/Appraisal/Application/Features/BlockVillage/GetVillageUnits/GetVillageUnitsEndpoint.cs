namespace Appraisal.Application.Features.BlockVillage.GetVillageUnits;

public class GetVillageUnitsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/village-units",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(new GetVillageUnitsQuery(appraisalId), cancellationToken);
                    return Results.Ok(result.Adapt<GetVillageUnitsResponse>());
                }
            )
            .WithName("GetVillageUnits")
            .Produces<GetVillageUnitsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get village units")
            .WithDescription("Gets all village units for an appraisal.")
            .WithTags("Block Village");
    }
}
