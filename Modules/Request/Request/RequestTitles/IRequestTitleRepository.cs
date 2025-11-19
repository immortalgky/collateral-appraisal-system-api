namespace Request.RequestTitles;

public interface IRequestTitleRepository
{
    Task<RequestTitle> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<RequestTitle>> GetByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task AddAsync(RequestTitle requestTitle, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<RequestTitle> requestTitles, CancellationToken cancellationToken = default);
    Task Remove(RequestTitle requestTitle);
    Task RemoveRangeAsync(IEnumerable<RequestTitle> requestTitles, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}