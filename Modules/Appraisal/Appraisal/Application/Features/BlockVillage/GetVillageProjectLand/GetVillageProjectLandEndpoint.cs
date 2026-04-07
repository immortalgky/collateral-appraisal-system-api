namespace Appraisal.Application.Features.BlockVillage.GetVillageProjectLand;

public class GetVillageProjectLandEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/village-project-land",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(new GetVillageProjectLandQuery(appraisalId), cancellationToken);

                    if (result is null)
                        return Results.NotFound();

                    return Results.Ok(result.Adapt<GetVillageProjectLandResponse>());
                }
            )
            .WithName("GetVillageProjectLand")
            .Produces<GetVillageProjectLandResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get village project land")
            .WithDescription("Gets the village project land details for an appraisal.")
            .WithTags("Block Village");
    }
}
