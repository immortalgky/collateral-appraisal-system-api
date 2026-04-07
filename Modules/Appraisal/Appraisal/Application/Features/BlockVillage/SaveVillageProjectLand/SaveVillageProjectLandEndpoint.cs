namespace Appraisal.Application.Features.BlockVillage.SaveVillageProjectLand;

public class SaveVillageProjectLandEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/village-project-land",
                async (
                    Guid appraisalId,
                    SaveVillageProjectLandRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<SaveVillageProjectLandCommand>()
                        with { AppraisalId = appraisalId };

                    var result = await sender.Send(command, cancellationToken);

                    return Results.Ok(result.Adapt<SaveVillageProjectLandResponse>());
                }
            )
            .WithName("SaveVillageProjectLand")
            .Produces<SaveVillageProjectLandResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Save village project land")
            .WithDescription("Creates or updates the village project land details for an appraisal.")
            .WithTags("Block Village");
    }
}
