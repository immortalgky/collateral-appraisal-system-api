using Carter;
using MediatR;

namespace Appraisal.Application.Features.Project.SetProjectModelImageThumbnail;

/// <summary>
/// Endpoint: PUT /appraisals/{appraisalId}/project/models/{modelId}/images/{imageId}/set-thumbnail
/// Sets the specified image as the thumbnail for the project model.
/// </summary>
public class SetProjectModelImageThumbnailEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/project/models/{modelId:guid}/images/{imageId:guid}/set-thumbnail",
                async (
                    Guid appraisalId,
                    Guid modelId,
                    Guid imageId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new SetProjectModelImageThumbnailCommand(modelId, imageId);

                    var result = await sender.Send(command, cancellationToken);

                    return Results.Ok(result);
                }
            )
            .WithName("SetProjectModelImageThumbnail")
            .Produces<SetProjectModelImageThumbnailResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Set project model image as thumbnail")
            .WithDescription(
                "Sets the specified image as the thumbnail for the project model. Automatically unsets any existing thumbnail.")
            .WithTags("ProjectModels");
    }
}
