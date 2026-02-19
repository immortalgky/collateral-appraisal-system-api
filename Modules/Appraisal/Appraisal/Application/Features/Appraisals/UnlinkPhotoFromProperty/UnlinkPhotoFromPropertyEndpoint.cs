using Carter;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.UnlinkPhotoFromProperty;

public class UnlinkPhotoFromPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/appraisals/{appraisalId:guid}/gallery/property-links/{mappingId:guid}",
                async (
                    Guid appraisalId,
                    Guid mappingId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new UnlinkPhotoFromPropertyCommand(mappingId);

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("UnlinkPhotoFromProperty")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Unlink photo from property")
            .WithDescription("Removes a photo-to-property mapping.")
            .WithTags("AppraisalGallery");
    }
}
