namespace Appraisal.Application.Features.BlockVillage.GetVillageProject;

public class GetVillageProjectEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/village-project",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(new GetVillageProjectQuery(appraisalId), cancellationToken);

                    if (result is null)
                        return Results.NotFound();

                    return Results.Ok(result.Adapt<GetVillageProjectResponse>());
                }
            )
            .WithName("GetVillageProject")
            .Produces<GetVillageProjectResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get village project")
            .WithDescription("Gets the village project for an appraisal.")
            .WithTags("Block Village");
    }
}
