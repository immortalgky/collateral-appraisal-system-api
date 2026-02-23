namespace Appraisal.Application.Features.Appraisals.GetPhotoTopics;

public class GetPhotoTopicsQueryHandler(
    AppraisalDbContext dbContext
) : IQueryHandler<GetPhotoTopicsQuery, GetPhotoTopicsResult>
{
    public async Task<GetPhotoTopicsResult> Handle(
        GetPhotoTopicsQuery query,
        CancellationToken cancellationToken)
    {
        var topics = await dbContext.PhotoTopics
            .Where(t => t.AppraisalId == query.AppraisalId)
            .OrderBy(t => t.SortOrder)
            .ToListAsync(cancellationToken);

        var topicIds = topics.Select(t => t.Id).ToList();

        // Query join table to find which photos belong to which topics
        var mappings = await dbContext.GalleryPhotoTopicMappings
            .Where(m => topicIds.Contains(m.PhotoTopicId))
            .ToListAsync(cancellationToken);

        var photoIds = mappings.Select(m => m.GalleryPhotoId).Distinct().ToList();

        var photos = await dbContext.AppraisalGallery
            .Where(g => photoIds.Contains(g.Id))
            .OrderBy(g => g.PhotoNumber)
            .ToListAsync(cancellationToken);

        var photosById = photos.ToDictionary(p => p.Id);

        var mappingsByTopic = mappings
            .GroupBy(m => m.PhotoTopicId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.GalleryPhotoId).ToList());

        var dtos = topics.Select(t =>
        {
            var topicPhotoIds = mappingsByTopic.GetValueOrDefault(t.Id, []);
            var topicPhotos = topicPhotoIds
                .Where(id => photosById.ContainsKey(id))
                .Select(id => photosById[id])
                .OrderBy(p => p.PhotoNumber)
                .ToList();

            return new PhotoTopicDto(
                t.Id,
                t.TopicName,
                t.SortOrder,
                t.DisplayColumns,
                topicPhotos.Count,
                topicPhotos.Select(p => new TopicPhotoDto(
                    p.Id,
                    p.DocumentId,
                    p.PhotoNumber,
                    p.Caption
                )).ToList()
            );
        }).ToList();

        return new GetPhotoTopicsResult(dtos);
    }
}
