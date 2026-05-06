namespace Appraisal.Application.Features.Project.DeleteProjectUnitUpload;

public class DeleteProjectUnitUploadEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/appraisals/{appraisalId:guid}/project/units/uploads/{uploadId:guid}",
                async (
                    Guid appraisalId,
                    Guid uploadId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new DeleteProjectUnitUploadCommand(appraisalId, uploadId);
                    await sender.Send(command, cancellationToken);
                    return Results.NoContent();
                }
            )
            .WithName("DeleteProjectUnitUpload")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete project unit upload")
            .WithDescription("Removes a unit upload batch and all its associated unit rows.")
            .WithTags("Project");
    }
}
