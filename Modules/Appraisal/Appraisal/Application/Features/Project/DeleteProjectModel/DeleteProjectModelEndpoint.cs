namespace Appraisal.Application.Features.Project.DeleteProjectModel;

public class DeleteProjectModelEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/appraisals/{appraisalId:guid}/project/models/{modelId:guid}",
                async (
                    Guid appraisalId,
                    Guid modelId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new DeleteProjectModelCommand(appraisalId, modelId);
                    await sender.Send(command, cancellationToken);
                    return Results.NoContent();
                }
            )
            .WithName("DeleteProjectModel")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete project model")
            .WithDescription("Removes a model from a project.")
            .WithTags("Project");
    }
}
