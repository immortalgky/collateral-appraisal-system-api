namespace Appraisal.Application.Features.Project.UpdateProjectModel;

public class UpdateProjectModelEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/project/models/{modelId:guid}",
                async (
                    Guid appraisalId,
                    Guid modelId,
                    UpdateProjectModelRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<UpdateProjectModelCommand>()
                        with { AppraisalId = appraisalId, ModelId = modelId };

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("UpdateProjectModel")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update project model")
            .WithDescription("Updates an existing project model.")
            .WithTags("Project");
    }
}
