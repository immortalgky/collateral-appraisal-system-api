using Carter;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.SetPropertyThumbnail;

public class SetPropertyThumbnailEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/photos/{photoId:guid}/set-thumbnail",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    Guid photoId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new SetPropertyThumbnailCommand(propertyId, photoId);

                    var result = await sender.Send(command, cancellationToken);

                    return Results.Ok(result);
                }
            )
            .WithName("SetPropertyThumbnail")
            .Produces<SetPropertyThumbnailResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Set property thumbnail")
            .WithDescription(
                "Sets the specified photo as the thumbnail for the property. Automatically unsets any existing thumbnail.")
            .WithTags("AppraisalGallery");
    }
}