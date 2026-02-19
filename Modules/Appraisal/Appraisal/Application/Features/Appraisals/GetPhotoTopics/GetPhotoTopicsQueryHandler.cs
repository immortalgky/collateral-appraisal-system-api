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

        var photos = await dbContext.AppraisalGallery
            .Where(g => g.PhotoTopicId != null && topicIds.Contains(g.PhotoTopicId.Value))
            .OrderBy(g => g.PhotoNumber)
            .ToListAsync(cancellationToken);

        var photosByTopic = photos
            .GroupBy(g => g.PhotoTopicId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var dtos = topics.Select(t =>
        {
            var topicPhotos = photosByTopic.GetValueOrDefault(t.Id, []);
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
