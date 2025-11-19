namespace Request.Data.Repository;

public class RequestTitleDocumentRepository(RequestDbContext dbContext) : IRequestTitleDocumentRepository
{
    public Task Remove(RequestTitleDocument requestTitleDocument)
    {
        dbContext.Remove(requestTitleDocument);
        return Task.CompletedTask;
    }

    public async Task SaveChangeAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<RequestTitleDocument> GetRequestTitleDocumentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.RequestTitleDocuments.FindAsync([id], cancellationToken);
    }
}