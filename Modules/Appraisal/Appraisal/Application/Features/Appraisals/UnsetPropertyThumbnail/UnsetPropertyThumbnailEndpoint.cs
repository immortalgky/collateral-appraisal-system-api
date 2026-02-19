using Carter;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.UnsetPropertyThumbnail;

public class UnsetPropertyThumbnailEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/photos/{photoId:guid}/unset-thumbnail",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    Guid photoId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new UnsetPropertyThumbnailCommand(propertyId, photoId);

                    var result = await sender.Send(command, cancellationToken);

                    return Results.Ok(result);
                }
            )
            .WithName("UnsetPropertyThumbnail")
            .Produces<UnsetPropertyThumbnailResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Unset property thumbnail")
            .WithDescription("Removes the thumbnail designation from the specified photo for the property.")
            .WithTags("AppraisalGallery");
    }
}
