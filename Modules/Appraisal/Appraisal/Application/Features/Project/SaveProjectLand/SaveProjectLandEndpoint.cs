namespace Appraisal.Application.Features.Project.SaveProjectLand;

public class SaveProjectLandEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/project/land",
                async (
                    Guid appraisalId,
                    SaveProjectLandRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<SaveProjectLandCommand>()
                        with { AppraisalId = appraisalId };

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<SaveProjectLandResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("SaveProjectLand")
            .Produces<SaveProjectLandResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Save project land")
            .WithDescription("Creates or updates land details for a LandAndBuilding project.")
            .WithTags("Project");
    }
}
