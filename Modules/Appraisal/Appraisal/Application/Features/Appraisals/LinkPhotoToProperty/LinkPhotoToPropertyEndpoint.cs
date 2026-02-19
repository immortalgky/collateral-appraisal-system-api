using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.LinkPhotoToProperty;

public class LinkPhotoToPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/gallery/{photoId:guid}/property-links",
                async (
                    Guid appraisalId,
                    Guid photoId,
                    LinkPhotoToPropertyRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new LinkPhotoToPropertyCommand(
                        photoId,
                        request.AppraisalPropertyId,
                        request.PhotoPurpose,
                        request.SectionReference,
                        request.LinkedBy);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<LinkPhotoToPropertyResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("LinkPhotoToProperty")
            .Produces<LinkPhotoToPropertyResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Link photo to property")
            .WithDescription("Creates a mapping between a gallery photo and a specific property detail section.")
            .WithTags("AppraisalGallery");
    }
}
