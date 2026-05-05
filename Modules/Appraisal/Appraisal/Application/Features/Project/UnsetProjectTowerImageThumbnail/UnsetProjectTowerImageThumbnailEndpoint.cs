using Carter;
using MediatR;

namespace Appraisal.Application.Features.Project.UnsetProjectTowerImageThumbnail;

/// <summary>
/// Endpoint: PUT /appraisals/{appraisalId}/project/towers/{towerId}/images/{imageId}/unset-thumbnail
/// Removes the thumbnail designation from the specified project tower image.
/// </summary>
public class UnsetProjectTowerImageThumbnailEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/project/towers/{towerId:guid}/images/{imageId:guid}/unset-thumbnail",
                async (
                    Guid appraisalId,
                    Guid towerId,
                    Guid imageId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new UnsetProjectTowerImageThumbnailCommand(towerId, imageId);

                    var result = await sender.Send(command, cancellationToken);

                    return Results.Ok(result);
                }
            )
            .WithName("UnsetProjectTowerImageThumbnail")
            .Produces<UnsetProjectTowerImageThumbnailResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Unset project tower image thumbnail")
            .WithDescription("Removes the thumbnail designation from the specified project tower image.")
            .WithTags("ProjectTowers");
    }
}
