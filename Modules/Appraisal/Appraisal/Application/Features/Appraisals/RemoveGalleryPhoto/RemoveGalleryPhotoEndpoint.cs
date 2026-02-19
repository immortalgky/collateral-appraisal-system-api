using Carter;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.RemoveGalleryPhoto;

public class RemoveGalleryPhotoEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/appraisals/{appraisalId:guid}/gallery/{photoId:guid}",
                async (
                    Guid appraisalId,
                    Guid photoId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new RemoveGalleryPhotoCommand(photoId);

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("RemoveGalleryPhoto")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Remove photo from gallery")
            .WithDescription("Removes a photo from the appraisal gallery and deletes any property photo mappings linked to it.")
            .WithTags("AppraisalGallery");
    }
}
