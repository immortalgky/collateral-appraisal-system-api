using Appraisal.Domain.Appraisals;
using Microsoft.EntityFrameworkCore;
using Shared.Data;

namespace Appraisal.Infrastructure.Repositories;

public class PhotoTopicRepository(AppraisalDbContext dbContext)
    : BaseRepository<PhotoTopic, Guid>(dbContext), IPhotoTopicRepository
{
    private readonly AppraisalDbContext _dbContext = dbContext;

    public async Task<IEnumerable<PhotoTopic>> GetByAppraisalIdAsync(Guid appraisalId, CancellationToken ct = default)
    {
        return await _dbContext.PhotoTopics
            .Where(t => t.AppraisalId == appraisalId)
            .OrderBy(t => t.SortOrder)
            .ToListAsync(ct);
    }

    public async Task<bool> HasPhotosAsync(Guid topicId, CancellationToken ct = default)
    {
        return await _dbContext.GalleryPhotoTopicMappings
            .AnyAsync(m => m.PhotoTopicId == topicId, ct);
    }
}
