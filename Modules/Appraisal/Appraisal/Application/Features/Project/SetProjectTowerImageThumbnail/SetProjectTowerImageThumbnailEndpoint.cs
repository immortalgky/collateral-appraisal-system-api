using Carter;
using MediatR;

namespace Appraisal.Application.Features.Project.SetProjectTowerImageThumbnail;

/// <summary>
/// Endpoint: PUT /appraisals/{appraisalId}/project/towers/{towerId}/images/{imageId}/set-thumbnail
/// Sets the specified image as the thumbnail for the project tower.
/// </summary>
public class SetProjectTowerImageThumbnailEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/project/towers/{towerId:guid}/images/{imageId:guid}/set-thumbnail",
                async (
                    Guid appraisalId,
                    Guid towerId,
                    Guid imageId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new SetProjectTowerImageThumbnailCommand(towerId, imageId);

                    var result = await sender.Send(command, cancellationToken);

                    return Results.Ok(result);
                }
            )
            .WithName("SetProjectTowerImageThumbnail")
            .Produces<SetProjectTowerImageThumbnailResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Set project tower image as thumbnail")
            .WithDescription(
                "Sets the specified image as the thumbnail for the project tower. Automatically unsets any existing thumbnail.")
            .WithTags("ProjectTowers");
    }
}
