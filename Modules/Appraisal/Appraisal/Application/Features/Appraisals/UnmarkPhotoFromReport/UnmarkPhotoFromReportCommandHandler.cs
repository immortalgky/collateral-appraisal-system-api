using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.UnmarkPhotoFromReport;

public class UnmarkPhotoFromReportCommandHandler(
    IAppraisalGalleryRepository galleryRepository
) : ICommandHandler<UnmarkPhotoFromReportCommand, UnmarkPhotoFromReportResult>
{
    public async Task<UnmarkPhotoFromReportResult> Handle(
        UnmarkPhotoFromReportCommand command,
        CancellationToken cancellationToken)
    {
        var photo = await galleryRepository.GetByIdAsync(command.PhotoId, cancellationToken);

        if (photo is null)
            throw new InvalidOperationException($"Gallery photo with ID {command.PhotoId} not found");

        photo.UnmarkFromReport();

        return new UnmarkPhotoFromReportResult(photo.Id);
    }
}
