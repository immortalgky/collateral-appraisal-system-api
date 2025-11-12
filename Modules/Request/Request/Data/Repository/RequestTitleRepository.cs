namespace Request.Data.Repository;

public class RequestTitleRepository(RequestDbContext dbContext) : IRequestTitleRepository
{
    public async Task<RequestTitle> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.RequestTitles.FindAsync([id], cancellationToken);
    }

    public async Task AddAsync(RequestTitle requestTitle, CancellationToken cancellationToken = default)
    {
        await dbContext.RequestTitles.AddAsync(requestTitle, cancellationToken);
    }

    public Task Remove(RequestTitle requestTitle)
    {
        dbContext.Remove(requestTitle);

        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}