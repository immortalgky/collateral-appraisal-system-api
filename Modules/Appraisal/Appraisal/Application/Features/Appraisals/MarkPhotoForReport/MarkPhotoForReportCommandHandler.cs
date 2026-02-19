using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.MarkPhotoForReport;

public class MarkPhotoForReportCommandHandler(
    IAppraisalGalleryRepository galleryRepository
) : ICommandHandler<MarkPhotoForReportCommand, MarkPhotoForReportResult>
{
    public async Task<MarkPhotoForReportResult> Handle(
        MarkPhotoForReportCommand command,
        CancellationToken cancellationToken)
    {
        var photo = await galleryRepository.GetByIdAsync(command.PhotoId, cancellationToken);

        if (photo is null)
            throw new InvalidOperationException($"Gallery photo with ID {command.PhotoId} not found");

        photo.MarkForReport(command.ReportSection);

        return new MarkPhotoForReportResult(photo.Id);
    }
}
