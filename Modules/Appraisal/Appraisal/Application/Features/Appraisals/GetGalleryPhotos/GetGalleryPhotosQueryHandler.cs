using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetGalleryPhotos;

public class GetGalleryPhotosQueryHandler(
    IAppraisalGalleryRepository galleryRepository,
    AppraisalDbContext dbContext
) : IQueryHandler<GetGalleryPhotosQuery, GetGalleryPhotosResult>
{
    public async Task<GetGalleryPhotosResult> Handle(
        GetGalleryPhotosQuery query,
        CancellationToken cancellationToken)
    {
        var photos = (await galleryRepository.GetByAppraisalIdAsync(
            query.AppraisalId, cancellationToken)).ToList();

        // Batch-load topic mappings for all photos
        var photoIds = photos.Select(p => p.Id).ToList();
        var topicMappings = await dbContext.GalleryPhotoTopicMappings
            .Where(m => photoIds.Contains(m.GalleryPhotoId))
            .ToListAsync(cancellationToken);

        var topicsByPhoto = topicMappings
            .GroupBy(m => m.GalleryPhotoId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.PhotoTopicId).ToList());

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
            topicsByPhoto.GetValueOrDefault(p.Id, [])
        )).ToList();

        return new GetGalleryPhotosResult(dtos);
    }
}
