using Carter;
using MediatR;

namespace Appraisal.Application.Features.Project.RemoveProjectModelImage;

/// <summary>
/// Endpoint: DELETE /appraisals/{appraisalId}/project/models/{modelId}/images/{imageId}
/// Removes an image from a project model.
/// </summary>
public class RemoveProjectModelImageEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/appraisals/{appraisalId:guid}/project/models/{modelId:guid}/images/{imageId:guid}",
                async (
                    Guid appraisalId,
                    Guid modelId,
                    Guid imageId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new RemoveProjectModelImageCommand(modelId, imageId);

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("RemoveProjectModelImage")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Remove image from project model")
            .WithDescription("Removes an image from a project model.")
            .WithTags("ProjectModels");
    }
}
