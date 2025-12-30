using Request.Domain.RequestTitles;

namespace Request.Infrastructure.Repositories;

public interface IRequestTitleRepository : IRepository<RequestTitle, Guid>
{
    Task<IEnumerable<RequestTitle>> GetByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<RequestTitle?> GetByIdWithDocumentsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<RequestTitle>> GetByRequestIdWithDocumentsAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default);
}
