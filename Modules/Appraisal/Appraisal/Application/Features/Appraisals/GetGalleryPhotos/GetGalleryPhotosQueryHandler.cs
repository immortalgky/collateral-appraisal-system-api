using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetGalleryPhotos;

public class GetGalleryPhotosQueryHandler(
    IAppraisalGalleryRepository galleryRepository
) : IQueryHandler<GetGalleryPhotosQuery, GetGalleryPhotosResult>
{
    public async Task<GetGalleryPhotosResult> Handle(
        GetGalleryPhotosQuery query,
        CancellationToken cancellationToken)
    {
        var photos = await galleryRepository.GetByAppraisalIdAsync(
            query.AppraisalId, cancellationToken);

        var dtos = photos.Select(p => new GalleryPhotoDto(
            p.Id,
            p.DocumentId,
            p.PhotoNumber,
            p.PhotoType,
            p.PhotoCategory,
            p.Caption,
            p.Latitude,
            p.Longitude,
            p.CapturedAt,
            p.UploadedAt,
            p.IsUsedInReport,
            p.ReportSection,
            p.PhotoTopicId
        )).ToList();

        return new GetGalleryPhotosResult(dtos);
    }
}
