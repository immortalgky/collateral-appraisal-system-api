using Carter;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.UnmarkPhotoFromReport;

public class UnmarkPhotoFromReportEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/gallery/{photoId:guid}/unmark-from-report",
                async (
                    Guid appraisalId,
                    Guid photoId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new UnmarkPhotoFromReportCommand(photoId);

                    var result = await sender.Send(command, cancellationToken);

                    return Results.Ok(result);
                }
            )
            .WithName("UnmarkPhotoFromReport")
            .Produces<UnmarkPhotoFromReportResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Unmark photo from report")
            .WithDescription("Removes a gallery photo from the appraisal report.")
            .WithTags("AppraisalGallery");
    }
}
