using Shared.Data;

namespace Appraisal.Domain.Appraisals;

public interface IPhotoTopicRepository : IRepository<PhotoTopic, Guid>
{
    Task<IEnumerable<PhotoTopic>> GetByAppraisalIdAsync(Guid appraisalId, CancellationToken ct = default);
    Task<bool> HasPhotosAsync(Guid topicId, CancellationToken ct = default);
}
