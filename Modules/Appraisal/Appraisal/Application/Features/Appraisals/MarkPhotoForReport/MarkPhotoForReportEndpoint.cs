using Carter;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.MarkPhotoForReport;

public class MarkPhotoForReportEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/gallery/{photoId:guid}/mark-for-report",
                async (
                    Guid appraisalId,
                    Guid photoId,
                    MarkPhotoForReportRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new MarkPhotoForReportCommand(photoId, request.ReportSection);

                    var result = await sender.Send(command, cancellationToken);

                    return Results.Ok(result);
                }
            )
            .WithName("MarkPhotoForReport")
            .Produces<MarkPhotoForReportResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Mark photo for report")
            .WithDescription("Marks a gallery photo to be included in the appraisal report under the specified section.")
            .WithTags("AppraisalGallery");
    }
}
