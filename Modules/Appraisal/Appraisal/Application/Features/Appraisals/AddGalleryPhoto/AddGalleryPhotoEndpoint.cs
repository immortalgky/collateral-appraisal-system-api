using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.AddGalleryPhoto;

public class AddGalleryPhotoEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/gallery",
                async (
                    Guid appraisalId,
                    AddGalleryPhotoRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new AddGalleryPhotoCommand(
                        appraisalId,
                        request.DocumentId,
                        request.PhotoType,
                        request.UploadedBy,
                        request.PhotoCategory,
                        request.Caption,
                        request.Latitude,
                        request.Longitude,
                        request.CapturedAt,
                        request.PhotoTopicIds);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<AddGalleryPhotoResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("AddGalleryPhoto")
            .Produces<AddGalleryPhotoResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Add photo to appraisal gallery")
            .WithDescription("Uploads a photo reference to the appraisal gallery. Document must be uploaded first via Document API.")
            .WithTags("AppraisalGallery");
    }
}
