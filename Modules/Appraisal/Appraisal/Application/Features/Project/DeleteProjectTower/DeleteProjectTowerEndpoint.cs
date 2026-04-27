namespace Appraisal.Application.Features.Project.DeleteProjectTower;

public class DeleteProjectTowerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/appraisals/{appraisalId:guid}/project/towers/{towerId:guid}",
                async (
                    Guid appraisalId,
                    Guid towerId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new DeleteProjectTowerCommand(appraisalId, towerId);
                    await sender.Send(command, cancellationToken);
                    return Results.NoContent();
                }
            )
            .WithName("DeleteProjectTower")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete project tower")
            .WithDescription("Removes a tower from a Condo project.")
            .WithTags("Project");
    }
}
