namespace Request.RequestTitles;

public interface IRequestTitleRepository
{
    Task<RequestTitle> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task AddAsync(RequestTitle requestTitle, CancellationToken cancellationToken = default);
    Task Remove(RequestTitle requestTitle);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}