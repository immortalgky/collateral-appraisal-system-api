namespace Appraisal.Application.Features.Project.UpdateProjectTower;

public class UpdateProjectTowerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/project/towers/{towerId:guid}",
                async (
                    Guid appraisalId,
                    Guid towerId,
                    UpdateProjectTowerRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<UpdateProjectTowerCommand>()
                        with { AppraisalId = appraisalId, TowerId = towerId };

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("UpdateProjectTower")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update project tower")
            .WithDescription("Updates an existing tower within a Condo project.")
            .WithTags("Project");
    }
}
