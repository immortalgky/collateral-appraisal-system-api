using Carter;
using MediatR;

namespace Appraisal.Application.Features.Project.UnsetProjectModelImageThumbnail;

/// <summary>
/// Endpoint: PUT /appraisals/{appraisalId}/project/models/{modelId}/images/{imageId}/unset-thumbnail
/// Removes the thumbnail designation from the specified project model image.
/// </summary>
public class UnsetProjectModelImageThumbnailEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/project/models/{modelId:guid}/images/{imageId:guid}/unset-thumbnail",
                async (
                    Guid appraisalId,
                    Guid modelId,
                    Guid imageId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new UnsetProjectModelImageThumbnailCommand(modelId, imageId);

                    var result = await sender.Send(command, cancellationToken);

                    return Results.Ok(result);
                }
            )
            .WithName("UnsetProjectModelImageThumbnail")
            .Produces<UnsetProjectModelImageThumbnailResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Unset project model image thumbnail")
            .WithDescription("Removes the thumbnail designation from the specified project model image.")
            .WithTags("ProjectModels");
    }
}
