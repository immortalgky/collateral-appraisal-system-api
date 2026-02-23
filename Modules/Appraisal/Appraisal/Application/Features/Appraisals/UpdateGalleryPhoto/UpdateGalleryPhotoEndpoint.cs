using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.UpdateGalleryPhoto;

public class UpdateGalleryPhotoEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/gallery/{photoId:guid}",
                async (
                    Guid appraisalId,
                    Guid photoId,
                    UpdateGalleryPhotoRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new UpdateGalleryPhotoCommand(
                        photoId,
                        request.PhotoCategory,
                        request.Caption,
                        request.Latitude,
                        request.Longitude,
                        request.CapturedAt);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<UpdateGalleryPhotoResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("UpdateGalleryPhoto")
            .Produces<UpdateGalleryPhotoResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update gallery photo details")
            .WithDescription("Updates category, caption, GPS coordinates, and captured date for a gallery photo.")
            .WithTags("AppraisalGallery");
    }
}
