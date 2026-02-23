using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.GetGalleryPhotos;

public class GetGalleryPhotosEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/gallery",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetGalleryPhotosQuery(appraisalId);

                    var result = await sender.Send(query, cancellationToken);

                    return Results.Ok(result);
                }
            )
            .WithName("GetGalleryPhotos")
            .Produces<GetGalleryPhotosResult>(StatusCodes.Status200OK)
            .WithSummary("Get all photos in appraisal gallery")
            .WithDescription("Returns all photos for the specified appraisal, ordered by photo number.")
            .WithTags("AppraisalGallery");
    }
}
