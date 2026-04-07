namespace Appraisal.Application.Features.BlockVillage.SaveVillageProject;

public class SaveVillageProjectEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/village-project",
                async (
                    Guid appraisalId,
                    SaveVillageProjectRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<SaveVillageProjectCommand>()
                        with { AppraisalId = appraisalId };

                    var result = await sender.Send(command, cancellationToken);

                    return Results.Ok(result.Adapt<SaveVillageProjectResponse>());
                }
            )
            .WithName("SaveVillageProject")
            .Produces<SaveVillageProjectResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Save village project")
            .WithDescription("Creates or updates the village project for an appraisal.")
            .WithTags("Block Village");
    }
}
